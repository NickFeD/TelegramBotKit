using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Telegram.Bot.Types;
using TelegramBotKit.Commands;
using TelegramBotKit.Conversations;
using TelegramBotKit.DependencyInjection;
using TelegramBotKit.Dispatching;
using TelegramBotKit.Hosting;
using TelegramBotKit.Middleware;
using Xunit;

namespace TelegramBotKit.Tests;

public sealed class ConversationDeliveryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Builtin_waiting_command_receives_response_through_middleware_without_deadlock(bool polling)
    {
        await using var runtime = new Runtime(polling);
        await runtime.StartAsync();
        runtime.Send(1, "/wait");
        await runtime.State.Waiting.Task.WaitAsync(TimeSpan.FromSeconds(5));
        runtime.Send(2, "/response");
        await runtime.Handled(2);
        await runtime.Handled(1);
        Assert.Equal("/response", (await runtime.State.Response.Task).Text);
        Assert.DoesNotContain("response command", runtime.State.Events);
        Assert.Equal(new[] { "global before 2", "global after 2", "disposed 2" }, runtime.EventsFor(2));
        Assert.Equal(new[] { "global before 1", "command resumed 1", "global after 1", "disposed 1" }, runtime.EventsFor(1));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Explicit_message_route_runs_local_middleware_and_terminal_instead_of_waiter(bool polling)
    {
        await using var runtime = new Runtime(polling, explicitRoute: true);
        using var waiterCancellation = new CancellationTokenSource();
        var waiting = runtime.Waiter.WaitAsync(123, 456, TimeSpan.FromSeconds(30), waiterCancellation.Token);
        await runtime.StartAsync();
        runtime.Send(2, "response");
        await runtime.Handled(2);
        Assert.False(waiting.IsCompleted);
        Assert.Equal(new[] { "global before 2", "local before 2", "terminal 2", "local after 2", "global after 2", "disposed 2" }, runtime.EventsFor(2));
        waiterCancellation.Cancel();
        Assert.Null(await waiting);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Global_short_circuit_does_not_publish_a_conversation_response(bool polling)
    {
        await using var runtime = new Runtime(polling, blockResponse: true);
        using var waiterCancellation = new CancellationTokenSource();
        var waiting = runtime.Waiter.WaitAsync(123, 456, TimeSpan.FromSeconds(30), waiterCancellation.Token);
        await runtime.StartAsync();
        runtime.Send(2, "response");
        await runtime.Handled(2);
        Assert.False(waiting.IsCompleted);
        Assert.Equal(new[] { "global before 2", "global after 2", "disposed 2" }, runtime.EventsFor(2));
        waiterCancellation.Cancel();
        Assert.Null(await waiting);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Waiter_cancellation_during_middleware_falls_back_without_repeating_pipeline(bool polling)
    {
        await using var runtime = new Runtime(polling);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        runtime.State.BeforeResponse = async ctx =>
        { entered.TrySetResult(); await release.Task.WaitAsync(ctx.CancellationToken); };
        using var waiterCancellation = new CancellationTokenSource();
        var waiting = runtime.Waiter.WaitAsync(123, 456, TimeSpan.FromSeconds(30), waiterCancellation.Token);
        await runtime.StartAsync();
        runtime.Send(2, "/response");
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        waiterCancellation.Cancel();
        Assert.Null(await waiting);
        release.TrySetResult();
        await runtime.Handled(2);
        Assert.Contains("response command", runtime.State.Events);
        Assert.Equal(new[] { "global before 2", "global after 2", "disposed 2" }, runtime.EventsFor(2));
    }

    private sealed class Runtime : IAsyncDisposable
    {
        private readonly bool _polling;
        private readonly FakePolling _httpHandler = new();
        private readonly HttpClient _http;
        private readonly CancellationTokenSource _ct = new();
        private readonly List<Task> _direct = new();
        private readonly ServiceProvider _provider;
        private readonly IHostedService? _hosted;
        public State State { get; } = new();
        public WaitForUserResponse Waiter => _provider.GetRequiredService<WaitForUserResponse>();

        public Runtime(bool polling, bool explicitRoute = false, bool blockResponse = false)
        {
            _polling = polling;
            _http = new HttpClient(_httpHandler);
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton(State);
            services.AddScoped<ScopeProbe>();
            var bot = services.AddTelegramBotKit(o =>
            {
                o.Token = "123456:TEST_TOKEN";
                // A response must bypass both the occupied chat actor and this slot.
                o.Polling.MaxDegreeOfParallelism = 1;
            }, _http);
            services.AddMessageCommand<WaitingCommand>("/wait");
            services.AddMessageCommand<ResponseCommand>("/response");
            bot.UseMiddleware((Func<BotContext, BotContextDelegate, Task>)(async (ctx, next) =>
            {
                var probe = ctx.Services.GetRequiredService<ScopeProbe>();
                probe.Id = ctx.Update.Id;
                State.Events.Enqueue("global before " + ctx.Update.Id);
                try
                {
                    if (ctx.Update.Id == 2 && State.BeforeResponse is not null)
                        await State.BeforeResponse(ctx);
                    if (!blockResponse || ctx.Update.Id != 2) await next(ctx);
                }
                finally
                {
                    Assert.False(probe.Disposed);
                    State.Events.Enqueue("global after " + ctx.Update.Id);
                }
            }));
            if (explicitRoute) bot.Route(UpdateRoutes.Message).Use<LocalMiddleware>().HandleWith<CustomHandler>();
            if (polling) services.AddTelegramBotKitPolling();
            _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
            _hosted = polling ? _provider.GetRequiredService<IHostedService>() : null;
        }

        public Task StartAsync() => _hosted?.StartAsync(CancellationToken.None) ?? Task.CompletedTask;
        public void Send(int id, string text)
        {
            if (_polling) _httpHandler.Send(id, text);
            else _direct.Add(_provider.GetRequiredService<IUpdateDispatcher>().DispatchAsync(new Update
            {
                Id = id, Message = new Message { Id = id, Text = text, Chat = new Chat { Id = 123 }, From = new User { Id = 456, FirstName = "Test" } }
            }, _ct.Token));
        }
        public Task Handled(int id) => State.Disposed(id).Task.WaitAsync(TimeSpan.FromSeconds(5));
        public string[] EventsFor(int id) => State.Events.Where(e => e.EndsWith(" " + id, StringComparison.Ordinal)).ToArray();
        public async ValueTask DisposeAsync()
        {
            _ct.Cancel();
            if (_hosted is not null) await _hosted.StopAsync(CancellationToken.None);
            try { await Task.WhenAll(_direct); } catch (OperationCanceledException) { }
            // Wait for scopes already started by polling before disposing the root provider.
            await Task.WhenAll(State.Started.Select(id => State.Disposed(id).Task)).WaitAsync(TimeSpan.FromSeconds(5));
            await _provider.DisposeAsync();
            _http.Dispose();
            _ct.Dispose();
        }
    }

    public sealed class State
    {
        public ConcurrentQueue<string> Events { get; } = new();
        public ConcurrentBag<int> Started { get; } = new();
        public TaskCompletionSource Waiting { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<Message> Response { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Func<BotContext, Task>? BeforeResponse { get; set; }
        private readonly ConcurrentDictionary<int, TaskCompletionSource> _disposed = new();
        public TaskCompletionSource Disposed(int id) => _disposed.GetOrAdd(id, _ => new(TaskCreationOptions.RunContinuationsAsynchronously));
    }
    public sealed class ScopeProbe(State state) : IAsyncDisposable
    {
        private int _id;
        public int Id { get => _id; set { _id = value; state.Started.Add(value); } }
        public bool Disposed { get; private set; }
        public ValueTask DisposeAsync()
        {
            Disposed = true;
            state.Events.Enqueue("disposed " + Id);
            state.Disposed(Id).TrySetResult();
            return ValueTask.CompletedTask;
        }
    }
    public sealed class WaitingCommand(WaitForUserResponse wait, State state, ScopeProbe probe) : IMessageCommand
    {
        public async Task HandleAsync(Message message, BotContext ctx)
        {
            var response = wait.WaitAsync(message.Chat.Id, message.From!.Id, TimeSpan.FromSeconds(20), ctx.CancellationToken);
            state.Waiting.TrySetResult();
            var result = await response;
            Assert.False(probe.Disposed);
            state.Events.Enqueue("command resumed " + ctx.Update.Id);
            if (result is not null) state.Response.TrySetResult(result);
        }
    }
    public sealed class ResponseCommand(State state) : IMessageCommand
    {
        public Task HandleAsync(Message message, BotContext ctx) { state.Events.Enqueue("response command"); return Task.CompletedTask; }
    }
    public sealed class CustomHandler(State state) : IUpdatePayloadHandler<Message>
    {
        public Task HandleAsync(Message payload, BotContext ctx) { state.Events.Enqueue("terminal " + ctx.Update.Id); return Task.CompletedTask; }
    }
    public sealed class LocalMiddleware(State state) : IUpdateRouteMiddleware<Message>
    {
        public async Task InvokeAsync(UpdateRouteContext<Message> context,
            UpdateRouteDelegate<Message> next)
        {
            var ctx = context.BotContext;
            state.Events.Enqueue("local before " + ctx.Update.Id);
            await next(context);
            state.Events.Enqueue("local after " + ctx.Update.Id);
        }
    }
    private sealed class FakePolling : HttpMessageHandler
    {
        private readonly Channel<string> _responses = Channel.CreateUnbounded<string>();
        public void Send(int id, string text) => _responses.Writer.TryWrite(JsonSerializer.Serialize(new
        {
            ok = true, result = new[] { new { update_id = id, message = new { message_id = id, date = 1,
                chat = new { id = 123, type = "private" }, from = new { id = 456, is_bot = false, first_name = "Test" }, text } } }
        }));
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.EndsWith("/getUpdates", request.RequestUri!.AbsolutePath, StringComparison.OrdinalIgnoreCase);
            var json = await _responses.Reader.ReadAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }
}
