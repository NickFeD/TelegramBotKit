using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot.Types;
using TelegramBotKit.DependencyInjection;
using TelegramBotKit.Dispatching;
using TelegramBotKit.Hosting;
using TelegramBotKit.Polling;
using TelegramBotKit.Options;
using Xunit;

namespace TelegramBotKit.Tests;

public sealed class SchedulerLifecycleTests
{
    [Fact]
    public async Task Receiver_preserves_admission_order_for_the_same_key()
    {
        var release = Signal();
        var calls = new ConcurrentQueue<int>();
        var dispatcher = new ObservedDispatcher(async (update, ct) =>
        {
            calls.Enqueue(update.Id);
            if (update.Id == 1) await release.Task.WaitAsync(ct);
        });
        await using var scheduler = Create(dispatcher);
        for (var id = 1; id <= 128; id++) await scheduler.ReceiveAsync(Update(id, 42));
        release.TrySetResult();
        await Task.WhenAll(Enumerable.Range(1, 128).Select(dispatcher.Finished));
        Assert.Equal(Enumerable.Range(1, 128), calls);
    }

    [Fact]
    public async Task Shared_execution_options_override_legacy_polling_concurrency()
    {
        var first = Signal();
        var second = Signal();
        var release = Signal();
        var dispatcher = new ObservedDispatcher(async (update, ct) =>
        {
            (update.Id == 1 ? first : second).TrySetResult();
            await release.Task.WaitAsync(ct);
        });
        var services = new ServiceCollection();
        services.AddLogging();
        services.Configure<TelegramBotKitOptions>(o => o.Polling.MaxDegreeOfParallelism = 1);
        services.AddSingleton<IUpdateDispatcher>(dispatcher);
        services.AddTelegramBotKitUpdateProcessing(configure: o => o.MaxDegreeOfParallelism = 2);
        await using var provider = services.BuildServiceProvider();
        var receiver = provider.GetRequiredService<IUpdateReceiver>();
        try
        {
            await receiver.ReceiveAsync(Update(1, 42));
            await first.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await receiver.ReceiveAsync(Update(2, 43));
            await second.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally { release.TrySetResult(); }
        await dispatcher.Finished(1);
        await dispatcher.Finished(2);
    }

    [Fact]
    public async Task Same_key_is_serialized_while_other_keys_can_run_in_parallel()
    {
        var entered = Signal();
        var release = Signal();
        var calls = new ConcurrentQueue<int>();
        var dispatcher = new ObservedDispatcher(async (update, ct) =>
        {
            calls.Enqueue(update.Id);
            if (update.Id == 1) { entered.SetResult(); await release.Task.WaitAsync(ct); }
        });
        await using var scheduler = Create(dispatcher, dop: 2);
        await scheduler.EnqueueAsync(Update(1, 42), default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await scheduler.EnqueueAsync(Update(2, 42), default);
        await scheduler.EnqueueAsync(Update(3, 43), default);
        await dispatcher.Finished(3);
        Assert.Equal(new[] { 1, 3 }, calls);
        release.SetResult();
        await dispatcher.Finished(2);
        Assert.Equal(new[] { 1, 3, 2 }, calls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Shutdown_cancels_and_awaits_active_and_queued_work(bool keyed)
    {
        var entered = Signal();
        var exited = Signal();
        var calls = new ConcurrentQueue<int>();
        var log = new ErrorLog();
        var dispatcher = new ObservedDispatcher(async (update, ct) =>
        {
            calls.Enqueue(update.Id);
            entered.TrySetResult();
            try { await Task.Delay(Timeout.InfiniteTimeSpan, ct); }
            finally { exited.TrySetResult(); }
        });
        await using var scheduler = Create(dispatcher, log: log);
        await scheduler.EnqueueAsync(Update(1, keyed ? 42 : null), default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await scheduler.EnqueueAsync(Update(2, keyed ? 42 : null), default);
        await scheduler.StopAsync(default).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(exited.Task.IsCompletedSuccessfully);
        Assert.Equal(new[] { 1 }, calls);
        Assert.Empty(log.Errors);
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await scheduler.EnqueueAsync(Update(3, 42), default));
    }

    [Fact]
    public async Task Stop_timeout_does_not_dispose_resources_used_by_running_work()
    {
        var entered = Signal();
        var release = Signal();
        var log = new ErrorLog();
        var dispatcher = new ObservedDispatcher(async (_, _) => { entered.SetResult(); await release.Task; });
        await using var scheduler = Create(dispatcher, log: log);
        await scheduler.EnqueueAsync(Update(1, 42), default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using var stopCancellation = new CancellationTokenSource();
        var stopping = scheduler.StopAsync(stopCancellation.Token);
        Assert.False(stopping.IsCompleted);
        stopCancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => stopping);
        release.SetResult();
        await scheduler.StopAsync(default).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Empty(log.Errors);
    }

    [Fact]
    public async Task Idle_cleanup_keeps_busy_actor_and_admits_work_after_retirement()
    {
        var clock = new TestTimeProvider();
        var entered = Signal();
        var release = Signal();
        var calls = new ConcurrentQueue<int>();
        var log = new ErrorLog();
        var dispatcher = new ObservedDispatcher(async (update, ct) =>
        {
            calls.Enqueue(update.Id);
            if (update.Id == 1) { entered.SetResult(); await release.Task.WaitAsync(ct); }
        });
        await using var scheduler = Create(dispatcher, clock: clock, log: log);
        await scheduler.EnqueueAsync(Update(1, 42), default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        clock.Advance(TimeSpan.FromMinutes(6));
        await scheduler.EnqueueAsync(Update(2, 42), default);
        Assert.Equal(new[] { 1 }, calls);
        release.SetResult();
        await dispatcher.Finished(2);
        clock.Advance(TimeSpan.FromMinutes(4));
        await scheduler.EnqueueAsync(Update(3, 42), default);
        await dispatcher.Finished(3);
        clock.Advance(TimeSpan.FromMinutes(5));
        await scheduler.EnqueueAsync(Update(4, 42), default);
        await dispatcher.Finished(4);
        Assert.Equal(new[] { 1, 2, 3, 4 }, calls);
        Assert.Empty(log.Errors);
    }

    [Fact]
    public async Task Concurrent_admission_and_cleanup_do_not_drop_updates()
    {
        var clock = new TestTimeProvider();
        var log = new ErrorLog();
        var calls = new ConcurrentBag<int>();
        var running = 0;
        var overlaps = 0;
        var dispatcher = new ObservedDispatcher(async (update, _) =>
        {
            if (Interlocked.Increment(ref running) != 1) Interlocked.Increment(ref overlaps);
            try { await Task.Yield(); calls.Add(update.Id); }
            finally { Interlocked.Decrement(ref running); }
        });
        await using var scheduler = Create(dispatcher, dop: 8, clock: clock, log: log);
        for (var round = 0; round < 20; round++)
        {
            var start = Signal();
            var writers = Enumerable.Range(round * 20 + 1, 20).Select(async id =>
            {
                await start.Task;
                await scheduler.EnqueueAsync(Update(id, 42), default);
            }).ToArray();
            var cleanup = Task.Run(async () => { await start.Task; clock.Advance(TimeSpan.FromMinutes(6)); });
            start.SetResult();
            await Task.WhenAll(writers.Append(cleanup));
            await Task.WhenAll(Enumerable.Range(round * 20 + 1, 20).Select(dispatcher.Finished));
        }
        Assert.Equal(Enumerable.Range(1, 400), calls.Order());
        Assert.Equal(0, overlaps);
        Assert.Empty(log.Errors);
    }

    [Fact]
    public async Task Plain_dispatcher_uses_keyed_scheduling_and_cooperative_shutdown()
    {
        var entered = Signal();
        var otherKey = Signal();
        var exited = Signal();
        var calls = new ConcurrentQueue<int>();
        var log = new ErrorLog();
        var dispatcher = new PlainDispatcher(async (update, ct) =>
        {
            calls.Enqueue(update.Id);
            if (update.Id == 1)
            {
                entered.SetResult();
                try { await Task.Delay(Timeout.InfiniteTimeSpan, ct); }
                finally { exited.SetResult(); }
            }
            else if (update.Id == 3) otherKey.SetResult();
        });
        await using var scheduler = Create(dispatcher, dop: 2, log: log);
        await scheduler.EnqueueAsync(Update(1, 42), default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await scheduler.EnqueueAsync(Update(2, 42), default);
        await scheduler.EnqueueAsync(Update(3, 43), default);
        await otherKey.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(new[] { 1, 3 }, calls);
        await scheduler.StopAsync(default).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(exited.Task.IsCompletedSuccessfully);
        Assert.Equal(new[] { 1, 3 }, calls);
        Assert.Empty(log.Errors);
    }

    [Fact]
    public async Task A_failed_update_does_not_block_the_next_update_and_is_logged_once()
    {
        var completed = Signal();
        var error = new InvalidOperationException("test dispatch failure");
        var log = new ErrorLog();
        var dispatcher = new ObservedDispatcher((update, _) =>
        {
            if (update.Id == 1) throw error;
            completed.SetResult();
            return Task.CompletedTask;
        });
        await using var scheduler = Create(dispatcher, log: log);
        await scheduler.EnqueueAsync(Update(1, 42), default);
        await scheduler.EnqueueAsync(Update(2, 42), default);
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await scheduler.StopAsync(default);
        Assert.Same(error, Assert.Single(log.Errors));
    }

    [Fact]
    public async Task Canceled_admission_does_not_invoke_the_dispatcher()
    {
        var calls = 0;
        var dispatcher = new ObservedDispatcher((_, _) => { calls++; return Task.CompletedTask; });
        await using var scheduler = Create(dispatcher);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await scheduler.EnqueueAsync(Update(1, 42), cancellation.Token));
        Assert.Equal(0, calls);
    }

    [Fact]
    public void Polling_registration_is_idempotent()
    {
        var services = new ServiceCollection();
        services.AddTelegramBotKitPolling();
        services.AddTelegramBotKitPolling();
        Assert.Single(services, d => d.ServiceType == typeof(UpdateActorScheduler));
        Assert.Equal(2, services.Count(d => d.ServiceType == typeof(IHostedService)));
    }

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static Update Update(int id, long? chat) => new()
    {
        Id = id,
        Message = chat is { } chatId ? new Message { Chat = new Chat { Id = chatId } } : null
    };
    private static UpdateActorScheduler Create(IUpdateDispatcher dispatcher, int dop = 1,
        TestTimeProvider? clock = null, ErrorLog? log = null) =>
        new(dispatcher, Microsoft.Extensions.Options.Options.Create(new TelegramBotKitOptions { Polling = new PollingOptions { MaxDegreeOfParallelism = dop } }),
            log ?? new ErrorLog(), clock ?? new TestTimeProvider());

    private sealed class ObservedDispatcher(Func<Update, CancellationToken, Task> dispatch) : IUpdateDispatcher, IScheduledUpdateDispatcher
    {
        private readonly ConcurrentDictionary<int, TaskCompletionSource> _finished = new();
        public Task DispatchAsync(Update update, CancellationToken ct = default) => dispatch(update, ct);
        public async Task DispatchScheduledAsync(Update update, CancellationToken ct, Func<Func<Task>, CancellationToken, Task> schedule)
        {
            try { await schedule(() => DispatchAsync(update, ct), ct); }
            finally { _finished.GetOrAdd(update.Id, _ => Signal()).TrySetResult(); }
        }
        internal Task Finished(int id) => _finished.GetOrAdd(id, _ => Signal()).Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    private sealed class PlainDispatcher(Func<Update, CancellationToken, Task> dispatch) : IUpdateDispatcher
    {
        public Task DispatchAsync(Update update, CancellationToken ct = default) => dispatch(update, ct);
    }

    private sealed class ErrorLog : ILogger<UpdateActorScheduler>
    {
        internal ConcurrentQueue<Exception> Errors { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Error && exception is not null) Errors.Enqueue(exception);
        }
    }
}
