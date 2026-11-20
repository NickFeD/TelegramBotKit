using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot.Types;
using TelegramBotKit.DependencyInjection;
using TelegramBotKit.Dispatching;
using TelegramBotKit.Hosting;

namespace TelegramBotKit.Benchmarks;

public sealed class BatchState
{
    private int _remaining;
    private TaskCompletionSource _finished = null!;
    internal int DelayMilliseconds;
    internal bool Yield;
    internal long[]? Starts, Ends;
    internal int Completed, Running, PeakRunning;
    internal Task Begin(int count)
    {
        Completed = Running = PeakRunning = 0;
        _remaining = count;
        _finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
        return _finished.Task;
    }
    internal async Task ExecuteAsync(Update update, CancellationToken ct)
    {
        var active = Interlocked.Increment(ref Running);
        int observed;
        do { observed = Volatile.Read(ref PeakRunning); }
        while (active > observed && Interlocked.CompareExchange(ref PeakRunning, active, observed) != observed);
        try
        {
            if (DelayMilliseconds > 0) await Task.Delay(DelayMilliseconds, ct);
            else if (Yield) await Task.Yield();
        }
        finally { Interlocked.Decrement(ref Running); }
    }
    internal void Complete(Update update)
    {
        if (Ends is not null) Ends[update.Id] = Stopwatch.GetTimestamp();
        Interlocked.Increment(ref Completed);
        if (Interlocked.Decrement(ref _remaining) == 0) _finished.TrySetResult();
    }
}

// Signals after Core has unwound middleware and disposed its per-update scope.
internal sealed class CompletingDispatcher(IUpdateDispatcher inner, BatchState state) : IUpdateDispatcher, IScheduledUpdateDispatcher
{
    public async Task DispatchAsync(Update update, CancellationToken ct = default)
    {
        await inner.DispatchAsync(update, ct);
        state.Complete(update);
    }
    public async Task DispatchScheduledAsync(Update update, CancellationToken ct, Func<Func<Task>, CancellationToken, Task> schedule)
    {
        if (inner is IScheduledUpdateDispatcher scheduled) await scheduled.DispatchScheduledAsync(update, ct, schedule);
        else await schedule(() => inner.DispatchAsync(update, ct), ct);
        state.Complete(update);
    }
}

internal sealed class SchedulerRuntime : IAsyncDisposable
{
    internal ServiceProvider Provider = null!;
    internal IUpdateReceiver Receiver = null!;
    internal IUpdateDispatcher Dispatcher = null!;
    internal BatchState State = null!;
    internal static SchedulerRuntime Create(int dop, bool yield = false, int delayMilliseconds = 0)
    {
        var (services, bot) = Fixtures.Services();
        var state = new BatchState { Yield = yield, DelayMilliseconds = delayMilliseconds };
        bot.Route(UpdateRoutes.Message).HandleWith<WorkHandler>();
        services.AddSingleton(state);
        // Preserve the real Core dispatcher and optional scheduling capability.
        var descriptor = services.Single(d => d.ServiceType == typeof(IUpdateDispatcher));
        services.Remove(descriptor);
        services.AddSingleton<IUpdateDispatcher>(sp => new CompletingDispatcher(
            (IUpdateDispatcher)ActivatorUtilities.CreateInstance(sp, descriptor.ImplementationType!), state));
        services.AddTelegramBotKitUpdateProcessing(configure: o => o.MaxDegreeOfParallelism = dop);
        var provider = services.BuildServiceProvider();
        return new SchedulerRuntime { Provider = provider, State = state,
            Receiver = provider.GetRequiredService<IUpdateReceiver>(), Dispatcher = provider.GetRequiredService<IUpdateDispatcher>() };
    }
    public async ValueTask DisposeAsync() => await Provider.DisposeAsync();
    public sealed class WorkHandler(BatchState state) : IUpdatePayloadHandler<Message>
    {
        public Task HandleAsync(Message payload, BotContext ctx) => state.ExecuteAsync(ctx.Update, ctx.CancellationToken);
    }
}

[ThreadingDiagnoser]
public class SchedulerBenchmarks
{
    public const int BatchSize = 256;
    [Params(1, 64)] public int Keys { get; set; }
    [Params(1, 8)] public int Dop { get; set; }
    [Params(false, true)] public bool Yield { get; set; }
    private SchedulerRuntime _runtime = null!;
    private Update[] _updates = null!;
    [GlobalSetup] public async Task Setup()
    {
        _runtime = SchedulerRuntime.Create(Dop, Yield);
        _updates = Enumerable.Range(0, BatchSize).Select(i => Fixtures.Update(chat: i % Keys + 1, id: i)).ToArray();
        await ReceiverBatch();
    }
    // A batch is the workload; BDN normalizes both methods to one completed update.
    [Benchmark(Baseline = true, OperationsPerInvoke = BatchSize)] public async Task CoreSequentialBatch()
    {
        var finished = _runtime.State.Begin(BatchSize);
        foreach (var update in _updates) await _runtime.Dispatcher.DispatchAsync(update);
        await finished.WaitAsync(TimeSpan.FromSeconds(30));
    }
    [Benchmark(OperationsPerInvoke = BatchSize)] public async Task ReceiverBatch()
    {
        var finished = _runtime.State.Begin(BatchSize);
        foreach (var update in _updates) await _runtime.Receiver.ReceiveAsync(update);
        await finished.WaitAsync(TimeSpan.FromSeconds(30));
    }
    [GlobalCleanup] public async Task Cleanup() => await _runtime.DisposeAsync();
}

internal static class SchedulerLoad
{
    internal static async Task RunAsync()
    {
        var results = new List<object>();
        foreach (var delay in new[] { 0, 1 })
        foreach (var producers in new[] { 1, 4 })
        foreach (var keys in new[] { 1, 256 })
        foreach (var dop in new[] { 1, 8 })
        {
            await using var runtime = SchedulerRuntime.Create(dop, delayMilliseconds: delay);
            var warmup = runtime.State.Begin(256);
            for (var i = 0; i < 256; i++) await runtime.Receiver.ReceiveAsync(Fixtures.Update(id: i, chat: i % keys + 1));
            await warmup.WaitAsync(TimeSpan.FromSeconds(60));
            for (var repeat = 1; repeat <= 3; repeat++)
            {
                var count = delay == 0 ? 20_000 : 512;
                var updates = Enumerable.Range(0, count).Select(i => Fixtures.Update(id: i, chat: i % keys + 1)).ToArray();
                var state = runtime.State;
                state.Starts = new long[count];
                state.Ends = new long[count];
                var finished = state.Begin(count);
                var allocatedBefore = GC.GetTotalAllocatedBytes(true);
                var gen0 = GC.CollectionCount(0);
                var peakBacklog = 0;
                var admitted = 0;
                var watch = Stopwatch.StartNew();
                await Task.WhenAll(Enumerable.Range(0, producers).Select(producer => Task.Run(async () =>
                {
                    for (var i = producer; i < count; i += producers)
                    {
                        state.Starts[i] = Stopwatch.GetTimestamp();
                        var backlog = Interlocked.Increment(ref admitted) - Volatile.Read(ref state.Completed);
                        int observed;
                        do { observed = Volatile.Read(ref peakBacklog); }
                        while (backlog > observed && Interlocked.CompareExchange(ref peakBacklog, backlog, observed) != observed);
                        await runtime.Receiver.ReceiveAsync(updates[i]);
                    }
                })));
                var admissionMs = watch.Elapsed.TotalMilliseconds;
                await finished.WaitAsync(TimeSpan.FromMinutes(2));
                watch.Stop();
                var allocated = GC.GetTotalAllocatedBytes(true) - allocatedBefore;
                var gc0 = GC.CollectionCount(0) - gen0;
                var latencies = state.Ends.Zip(state.Starts, (end, start) => (end - start) * 1000d / Stopwatch.Frequency).Order().ToArray();
                if (state.Completed != count || state.Ends.Any(t => t == 0)) throw new InvalidOperationException("Incomplete workload.");
                var result = new { keys, dop, producers, delayMs = delay, repeat, count, completed = state.Completed,
                    admissionMs, elapsedMs = watch.Elapsed.TotalMilliseconds, updatesPerSecond = count / watch.Elapsed.TotalSeconds,
                    p50Ms = latencies[(int)((count - 1) * .50)], p95Ms = latencies[(int)((count - 1) * .95)],
                    p99Ms = latencies[(int)((count - 1) * .99)], maxMs = latencies[^1],
                    allocatedBytesPerUpdate = (double)allocated / count, gen0 = gc0, peakBacklog, peakRunning = state.PeakRunning };
                results.Add(result);
                Console.WriteLine(JsonSerializer.Serialize(result));
                state.Starts = state.Ends = null;
            }
        }
        var directory = Path.Combine("BenchmarkDotNet.Artifacts", "load-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "results.json"), JsonSerializer.Serialize(new
        {
            utc = DateTime.UtcNow, runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            os = System.Runtime.InteropServices.RuntimeInformation.OSDescription, processors = Environment.ProcessorCount, results
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine(directory);
    }
}
