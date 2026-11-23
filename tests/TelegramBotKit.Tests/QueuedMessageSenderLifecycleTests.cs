using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using TelegramBotKit.Messaging;
using Xunit;

namespace TelegramBotKit.Tests;

public sealed class QueuedMessageSenderLifecycleTests
{
    [Fact]
    public async Task Shutdown_completes_active_buffered_and_backpressured_operations()
    {
        using var handler = new GateHandler();
        using var http = new HttpClient(handler);
        await using var sender = Create(http);
        var active = sender.SendText(1, new SendText { Text = "active" });
        await handler.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var buffered = sender.AnswerCallback("callback", new AnswerCallback());
        var backpressured = sender.SendText(2, new SendText { Text = "waiting for space" });

        await sender.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));

        foreach (var operation in new Task[] { active, buffered, backpressured })
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Caller_can_cancel_buffered_operation_without_waiting_for_active_request(bool acknowledgment)
    {
        using var handler = new GateHandler();
        using var http = new HttpClient(handler);
        await using var sender = Create(http);
        var active = sender.SendText(1, new SendText { Text = "active" });
        await handler.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using var cancellation = new CancellationTokenSource();
        Task queued = acknowledgment
            ? sender.AnswerCallback("callback", new AnswerCallback(), cancellation.Token)
            : sender.SendText(2, new SendText { Text = "canceled" }, cancellation.Token);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.False(active.IsCompleted);
        handler.Release.TrySetResult();
        Assert.Equal(10, (await active.WaitAsync(TimeSpan.FromSeconds(5))).Id);
        await sender.DisposeAsync();
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Caller_can_cancel_backpressured_operation_without_canceling_other_messages()
    {
        using var handler = new GateHandler();
        using var http = new HttpClient(handler);
        await using var sender = Create(http);
        var active = sender.SendText(1, new SendText { Text = "active" });
        await handler.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var buffered = sender.SendText(2, new SendText { Text = "buffered" });
        using var cancellation = new CancellationTokenSource();
        var blocked = sender.SendText(3, new SendText { Text = "not admitted" }, cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => blocked.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.False(active.IsCompleted);
        Assert.False(buffered.IsCompleted);
        handler.Release.TrySetResult();
        await Task.WhenAll(active, buffered).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task Shutdown_is_idempotent_and_new_operations_are_canceled()
    {
        using var handler = new GateHandler();
        using var http = new HttpClient(handler);
        var sender = Create(http);
        await Task.WhenAll(sender.DisposeAsync().AsTask(), sender.DisposeAsync().AsTask());
        var operation = sender.SendText(1, new SendText { Text = "after shutdown" });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation.WaitAsync(TimeSpan.FromSeconds(2)));
        await sender.DisposeAsync();
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Throwing_transport_cancellation_callback_cannot_leave_admission_open()
    {
        using var handler = new GateHandler(throwOnCancellation: true);
        using var http = new HttpClient(handler);
        await using var sender = Create(http);
        var active = sender.SendText(1, new SendText { Text = "active" });
        await handler.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await sender.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => active.WaitAsync(TimeSpan.FromSeconds(2)));
        var afterShutdown = sender.SendText(2, new SendText { Text = "after shutdown" });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => afterShutdown.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Already_canceled_operation_does_not_reach_transport()
    {
        using var handler = new GateHandler();
        using var http = new HttpClient(handler);
        await using var sender = Create(http);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sender.SendText(1, new SendText { Text = "canceled" }, cancellation.Token));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Non_retryable_error_fails_only_its_operation_and_worker_continues()
    {
        using var handler = new SequenceHandler(1, false);
        using var http = new HttpClient(handler);
        await using var sender = Create(http);
        var error = await Assert.ThrowsAsync<RequestException>(() => sender.SendText(1, new SendText { Text = "bad" }).WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.IsType<InvalidOperationException>(error.InnerException);
        Assert.Equal(10, (await sender.SendText(2, new SendText { Text = "good" }).WaitAsync(TimeSpan.FromSeconds(5))).Id);
        Assert.Equal(2, handler.Calls);
    }

    [Theory]
    [InlineData(2, true)]
    [InlineData(3, false)]
    public async Task Retry_limit_counts_initial_attempt_and_preserves_success_or_failure(int failures, bool succeeds)
    {
        using var handler = new SequenceHandler(failures, true);
        using var http = new HttpClient(handler);
        await using var sender = Create(http, attempts: 3);
        var operation = sender.SendText(1, new SendText { Text = "retry" });
        if (succeeds) Assert.Equal(10, (await operation.WaitAsync(TimeSpan.FromSeconds(5))).Id);
        else
        {
            var error = await Assert.ThrowsAsync<RequestException>(() => operation.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.IsType<HttpRequestException>(error.InnerException);
        }
        Assert.Equal(3, handler.Calls);
    }

    private static QueuedMessageSender Create(HttpClient http, int attempts = 1) => new(
        new MessageSender(new TelegramBotClient("12345:TEST", http)),
        Microsoft.Extensions.Options.Options.Create(new QueuedMessageSenderOptions
        {
            MaxQueueSize = 1, GlobalMaxPerSecond = 0, PerChatMinDelay = TimeSpan.Zero,
            MaxRetryAttempts = attempts, ServerErrorBaseDelay = TimeSpan.FromMilliseconds(1),
            ServerErrorMaxDelay = TimeSpan.FromMilliseconds(1)
        }), NullLogger<QueuedMessageSender>.Instance);

    private static HttpResponseMessage Success() => new(HttpStatusCode.OK)
    {
        Content = new StringContent("""{"ok":true,"result":{"message_id":10,"date":1700000000,"chat":{"id":1,"type":"private"},"text":"sent"}}""", Encoding.UTF8, "application/json")
    };

    private sealed class GateHandler(bool throwOnCancellation = false) : HttpMessageHandler
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _calls;
        public int Calls => Volatile.Read(ref _calls);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            using var registration = throwOnCancellation
                ? cancellationToken.Register(static () => throw new InvalidOperationException("Cancellation callback failure."))
                : default;
            Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return Success();
        }
    }

    private sealed class SequenceHandler(int failures, bool retryable) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (++Calls <= failures)
            {
                if (retryable) throw new HttpRequestException("Temporary transport failure.");
                throw new InvalidOperationException("Invalid request.");
            }
            return Task.FromResult(Success());
        }
    }
}
