using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramBotKit.Commands;
using TelegramBotKit.Conversations;
using TelegramBotKit.DependencyInjection;
using TelegramBotKit.Hosting;
using TelegramBotKit.Polling;
using TelegramBotKit.Middleware;
using TelegramBotKit.Options;
using TelegramBotKit.Webhook.AspNetCore;
using Xunit;

namespace TelegramBotKit.Tests;

public sealed class WebhookEndpointTests
{
    [Fact]
    public async Task Valid_update_runs_core_pipeline_and_disposes_its_own_scope()
    {
        await using var runtime = await Runtime.StartAsync();
        using var response = await runtime.Post(MessageJson(1, "/echo"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await runtime.State.Finished(1);
        Assert.Equal(new[] { "before 1", "handler 1", "after 1", "disposed 1" }, runtime.State.Events);
        Assert.Empty(runtime.Api.Methods);
    }

    [Fact]
    public async Task Waiting_command_receives_webhook_response_with_DOP_one()
    {
        await using var runtime = await Runtime.StartAsync();
        using var command = await runtime.Post(MessageJson(1, "/wait"));
        Assert.Equal(HttpStatusCode.OK, command.StatusCode);
        await runtime.State.Waiting.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using var reply = await runtime.Post(MessageJson(2, "answer"));
        Assert.Equal(HttpStatusCode.OK, reply.StatusCode);
        await runtime.State.Finished(1);
        await runtime.State.Finished(2);
        Assert.Equal("answer", (await runtime.State.Response.Task).Text);
        Assert.Contains("before 2", runtime.State.Events);
        Assert.DoesNotContain("handler 2", runtime.State.Events);
    }

    [Fact]
    public async Task Accepted_work_outlives_admission_cancellation_and_shutdown_awaits_disposal()
    {
        await using var runtime = await Runtime.StartAsync();
        using var admission = new CancellationTokenSource();
        await runtime.App.Services.GetRequiredService<IUpdateReceiver>().ReceiveAsync(
            JsonSerializer.Deserialize<Update>(MessageJson(1, "/wait"), Telegram.Bot.JsonBotAPI.Options)!, admission.Token);
        await runtime.State.Waiting.Task.WaitAsync(TimeSpan.FromSeconds(5));
        admission.Cancel();
        Assert.False(runtime.State.Response.Task.IsCompleted);
        await runtime.App.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));
        await runtime.State.Finished(1);
        Assert.True(runtime.State.Canceled);
    }

    [Fact]
    public async Task Http_success_does_not_wait_for_synchronous_application_middleware()
    {
        using var release = new ManualResetEventSlim();
        var entered = Signal();
        await using var runtime = await Runtime.StartAsync(extraMiddleware: (_, _) =>
        {
            entered.TrySetResult();
            if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("test gate not released");
            return Task.CompletedTask;
        });
        try
        {
            using var response = await runtime.Post(MessageJson(1, "/echo")).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally { release.Set(); }
        await runtime.State.Finished(1);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("wrong-secret")]
    [InlineData("test-secret,test-secret")]
    public async Task Missing_or_wrong_secret_never_reaches_receiver(string? secret)
    {
        var receiver = new RecordingReceiver();
        await using var runtime = await Runtime.StartAsync(receiver: receiver);
        using var response = await runtime.Post(MessageJson(1, "/echo"), secret: secret);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(receiver.Updates);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"update_id\":\"1\"}")]
    [InlineData("{\"update_id\":-1}")]
    [InlineData("{\"update_id\":true}")]
    [InlineData("{\"update_id\":1.5}")]
    [InlineData("{")]
    public async Task Malformed_update_is_rejected_before_admission(string body)
    {
        var receiver = new RecordingReceiver();
        await using var runtime = await Runtime.StartAsync(receiver: receiver);
        using var response = await runtime.Post(body);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(receiver.Updates);
    }

    [Fact]
    public async Task Non_json_content_is_rejected()
    {
        var receiver = new RecordingReceiver();
        await using var runtime = await Runtime.StartAsync(receiver: receiver);
        using var response = await runtime.Post("text", mediaType: "text/plain");
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.Empty(receiver.Updates);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Body_limit_applies_with_and_without_content_length(bool unknownLength)
    {
        var receiver = new RecordingReceiver();
        await using var runtime = await Runtime.StartAsync(o => o.MaxRequestBodyBytes = 32, receiver);
        using var request = Request(new string(' ', 33), unknownLength);
        using var response = await runtime.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Empty(receiver.Updates);
    }

    [Fact]
    public async Task Exact_body_limit_and_custom_path_accept_a_future_update()
    {
        const string body = "{\"update_id\":123,\"future_payload\":{\"value\":1}}";
        var receiver = new RecordingReceiver();
        await using var runtime = await Runtime.StartAsync(o =>
        {
            o.Path = "/custom-hook";
            o.MaxRequestBodyBytes = Encoding.UTF8.GetByteCount(body);
        }, receiver);
        using var request = Request(body, path: "/custom-hook");
        using var response = await runtime.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(123, Assert.Single(receiver.Updates).Id);
    }

    [Fact]
    public async Task Stopped_execution_returns_service_unavailable()
    {
        await using var runtime = await Runtime.StartAsync();
        var scheduler = runtime.App.Services.GetRequiredService<UpdateActorScheduler>();
        await scheduler.StopAsync(default);
        using var response = await runtime.Post(MessageJson(1, "/echo"));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Webhook_management_is_explicit_and_sends_configured_parameters()
    {
        await using var runtime = await Runtime.StartAsync(o =>
        {
            o.Url = "https://bot.example/telegram/update";
            o.MaxConnections = 3;
            o.AllowedUpdates = [UpdateType.Message];
            o.DropPendingUpdates = true;
        });
        Assert.Empty(runtime.Api.Methods);
        await runtime.App.Services.SetTelegramBotKitWebhookAsync();
        Assert.Equal("setWebhook", Assert.Single(runtime.Api.Methods));
        var body = Assert.Single(runtime.Api.Bodies);
        Assert.Equal("https://bot.example/telegram/update", body.GetProperty("url").GetString());
        Assert.Equal("test-secret", body.GetProperty("secret_token").GetString());
        Assert.Equal(3, body.GetProperty("max_connections").GetInt32());
        Assert.True(body.GetProperty("drop_pending_updates").GetBoolean());
        Assert.Equal("message", body.GetProperty("allowed_updates")[0].GetString());
        await runtime.App.StopAsync();
        Assert.Single(runtime.Api.Methods);
        await runtime.App.Services.DeleteTelegramBotKitWebhookAsync();
        Assert.Equal(new[] { "setWebhook", "deleteWebhook" }, runtime.Api.Methods);
        var deletion = runtime.Api.Bodies.Last();
        Assert.False(deletion.TryGetProperty("drop_pending_updates", out var dropPending) && dropPending.GetBoolean());
        await runtime.App.Services.DeleteTelegramBotKitWebhookAsync(dropPendingUpdates: true);
        Assert.True(runtime.Api.Bodies.Last().GetProperty("drop_pending_updates").GetBoolean());
    }

    [Fact]
    public async Task Explicit_management_requires_a_public_url_without_contacting_Telegram()
    {
        await using var runtime = await Runtime.StartAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => runtime.App.Services.SetTelegramBotKitWebhookAsync());
        Assert.Empty(runtime.Api.Methods);
    }

    [Fact]
    public async Task Other_HTTP_methods_do_not_admit_updates()
    {
        var receiver = new RecordingReceiver();
        await using var runtime = await Runtime.StartAsync(receiver: receiver);
        using var request = Request(MessageJson(1, "/echo"));
        request.Method = HttpMethod.Get;
        using var response = await runtime.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Empty(receiver.Updates);
    }

    [Fact]
    public async Task Duplicate_updates_are_not_silently_deduplicated_by_the_in_memory_receiver_contract()
    {
        var receiver = new RecordingReceiver();
        await using var runtime = await Runtime.StartAsync(receiver: receiver);
        using var first = await runtime.Post(MessageJson(1, "/echo"));
        using var repeated = await runtime.Post(MessageJson(1, "/echo"));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
        Assert.Equal(new[] { 1, 1 }, receiver.Updates.Select(u => u.Id));
    }

    [Theory]
    [InlineData("secret")]
    [InlineData("path")]
    [InlineData("url")]
    [InlineData("connections")]
    [InlineData("limit")]
    public async Task Invalid_configuration_fails_before_host_start(string kind)
    {
        await Assert.ThrowsAsync<OptionsValidationException>(async () =>
        {
            await using var runtime = await Runtime.StartAsync(o =>
            {
                switch (kind)
                {
                    case "secret": o.SecretToken = "invalid secret"; break;
                    case "path": o.Path = "relative"; break;
                    case "url": o.Url = "http://bot.example/hook"; break;
                    case "connections": o.MaxConnections = 0; break;
                    case "limit": o.MaxRequestBodyBytes = 0; break;
                }
            });
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Polling_and_webhook_cannot_be_registered_together(bool pollingFirst)
    {
        var services = new ServiceCollection();
        if (pollingFirst)
        {
            services.AddTelegramBotKitPolling();
            Assert.Throws<InvalidOperationException>(() => services.AddTelegramBotKitWebhook(o => o.SecretToken = "secret"));
        }
        else
        {
            services.AddTelegramBotKitWebhook(o => o.SecretToken = "secret");
            Assert.Throws<InvalidOperationException>(() => services.AddTelegramBotKitPolling());
        }
    }

    [Fact]
    public void Webhook_registration_is_idempotent_and_keeps_custom_receiver()
    {
        var services = new ServiceCollection();
        var receiver = new RecordingReceiver();
        services.AddSingleton<IUpdateReceiver>(receiver);
        services.AddTelegramBotKitWebhook(o => o.SecretToken = "secret");
        services.AddTelegramBotKitWebhook(o => o.Path = "/hook");
        Assert.Single(services, d => d.ServiceType == typeof(IUpdateReceiver));
        Assert.Same(receiver, services.Single(d => d.ServiceType == typeof(IUpdateReceiver)).ImplementationInstance);
        Assert.Single(services, d => d.ServiceType == typeof(IHostedService));
    }

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static string MessageJson(int id, string text) => $$$"""
        {"update_id":{{{id}}},"message":{"message_id":{{{id}}},"date":1,"chat":{"id":42,"type":"private"},"from":{"id":7,"is_bot":false,"first_name":"Test"},"text":"{{{text}}}"}}
        """;

    private static HttpRequestMessage Request(string body, bool unknownLength = false, string path = "/telegram/update",
        string? secret = "test-secret", string mediaType = "application/json")
    {
        var content = unknownLength ? (HttpContent)new UnknownLengthContent(body) : new StringContent(body, Encoding.UTF8, mediaType);
        content.Headers.ContentType = new(mediaType);
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = content };
        if (secret is not null) request.Headers.Add("X-Telegram-Bot-Api-Secret-Token", secret);
        return request;
    }

    private sealed class UnknownLengthContent(string body) : HttpContent
    {
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            stream.WriteAsync(Encoding.UTF8.GetBytes(body)).AsTask();
    }

    private sealed class RecordingReceiver : IUpdateReceiver
    {
        internal List<Update> Updates { get; } = new();
        public ValueTask ReceiveAsync(Update update, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            Updates.Add(update);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class Runtime : IAsyncDisposable
    {
        internal required WebApplication App { get; init; }
        internal required HttpClient Client { get; init; }
        internal required HttpClient BotHttp { get; init; }
        internal required ManagementApi Api { get; init; }
        internal required State State { get; init; }

        internal static async Task<Runtime> StartAsync(Action<WebhookOptions>? configure = null,
            IUpdateReceiver? receiver = null, Func<BotContext, BotContextDelegate, Task>? extraMiddleware = null)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
            builder.WebHost.UseTestServer();
            var api = new ManagementApi();
            var botHttp = new HttpClient(api);
            var state = new State();
            builder.Services.AddSingleton(state);
            builder.Services.AddScoped<ScopeProbe>();
            if (receiver is not null) builder.Services.AddSingleton(receiver);
            var bot = builder.Services.AddTelegramBotKit(o =>
            {
                o.Token = "123456:TEST_TOKEN";
                o.Polling.MaxDegreeOfParallelism = 1;
            }, botHttp);
            bot.UseMiddleware((Func<BotContext, BotContextDelegate, Task>)(async (ctx, next) =>
            {
                var probe = ctx.Services.GetRequiredService<ScopeProbe>();
                probe.Id = ctx.Update.Id;
                state.Events.Enqueue("before " + ctx.Update.Id);
                try
                {
                    if (extraMiddleware is null) await next(ctx);
                    else await extraMiddleware(ctx, next);
                }
                finally { state.Events.Enqueue("after " + ctx.Update.Id); }
            }));
            builder.Services.AddMessageCommand<EchoCommand>("/echo");
            builder.Services.AddMessageCommand<WaitingCommand>("/wait");
            builder.Services.AddTelegramBotKitWebhook(o => { o.SecretToken = "test-secret"; configure?.Invoke(o); });
            var app = builder.Build();
            try
            {
                app.MapTelegramBotKitWebhook();
                await app.StartAsync();
                return new Runtime { App = app, Client = app.GetTestClient(), BotHttp = botHttp, Api = api, State = state };
            }
            catch { await app.DisposeAsync(); botHttp.Dispose(); throw; }
        }

        internal async Task<HttpResponseMessage> Post(string body, string? secret = "test-secret", string mediaType = "application/json")
        {
            using var request = Request(body, secret: secret, mediaType: mediaType);
            return await Client.SendAsync(request);
        }

        public async ValueTask DisposeAsync()
        {
            await App.StopAsync();
            await App.DisposeAsync();
            Client.Dispose();
            BotHttp.Dispose();
        }
    }

    public sealed class State
    {
        public ConcurrentQueue<string> Events { get; } = new();
        public TaskCompletionSource Waiting { get; } = Signal();
        public TaskCompletionSource<Message> Response { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Canceled { get; set; }
        private readonly ConcurrentDictionary<int, TaskCompletionSource> _finished = new();
        public Task Finished(int id) => _finished.GetOrAdd(id, _ => Signal()).Task.WaitAsync(TimeSpan.FromSeconds(5));
        public void Complete(int id) => _finished.GetOrAdd(id, _ => Signal()).TrySetResult();
    }

    public sealed class ScopeProbe(State state) : IAsyncDisposable
    {
        public int Id { get; set; }
        public ValueTask DisposeAsync()
        {
            state.Events.Enqueue("disposed " + Id);
            state.Complete(Id);
            return ValueTask.CompletedTask;
        }
    }

    public sealed class EchoCommand(State state) : IMessageCommand
    {
        public Task HandleAsync(Message message, BotContext ctx)
        {
            state.Events.Enqueue("handler " + ctx.Update.Id);
            return Task.CompletedTask;
        }
    }

    public sealed class WaitingCommand(WaitForUserResponse wait, State state) : IMessageCommand
    {
        public async Task HandleAsync(Message message, BotContext ctx)
        {
            var response = wait.WaitAsync(message.Chat.Id, message.From!.Id, TimeSpan.FromMinutes(1), ctx.CancellationToken);
            state.Waiting.TrySetResult();
            var received = await response;
            state.Canceled = ctx.CancellationToken.IsCancellationRequested;
            if (received is not null) state.Response.TrySetResult(received);
        }
    }

    private sealed class ManagementApi : HttpMessageHandler
    {
        internal List<string> Methods { get; } = new();
        internal List<JsonElement> Bodies { get; } = new();
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Methods.Add(request.RequestUri!.Segments[^1]);
            using var document = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            Bodies.Add(document.RootElement.Clone());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"ok\":true,\"result\":true}", Encoding.UTF8, "application/json")
            };
        }
    }
}
