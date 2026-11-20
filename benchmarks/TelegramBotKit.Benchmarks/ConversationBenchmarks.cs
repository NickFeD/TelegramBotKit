using BenchmarkDotNet.Attributes;
using Telegram.Bot.Types;
using TelegramBotKit.Conversations;

namespace TelegramBotKit.Benchmarks;

public class ConversationBenchmarks
{
    [Params(0, 1000)] public int ActiveWaiters { get; set; }
    private WaitForUserResponse _wait = null!;
    private readonly Message _message = Fixtures.Update().Message!;
    private CancellationTokenSource _lifetime = null!;
    private Task<Message?>[] _pending = null!;
    [GlobalSetup] public void Setup()
    {
        _wait = new WaitForUserResponse();
        _lifetime = new CancellationTokenSource();
        _pending = Enumerable.Range(100, ActiveWaiters).Select(i =>
            _wait.WaitAsync(i, 7, TimeSpan.FromHours(1), _lifetime.Token)).ToArray();
    }
    [Benchmark] public bool PublishMiss() => _wait.TryPublish(_message);
    [Benchmark] public async Task<Message?> RegisterAndRespond()
    {
        var response = _wait.WaitAsync(42, 7, TimeSpan.FromHours(1));
        if (!_wait.TryPublish(_message)) throw new InvalidOperationException("Response was not delivered.");
        return await response;
    }
    [Benchmark] public async Task<Message?> RegisterAndCancel()
    {
        using var ct = new CancellationTokenSource();
        var response = _wait.WaitAsync(42, 7, TimeSpan.FromHours(1), ct.Token);
        ct.Cancel();
        return await response;
    }
    [GlobalCleanup] public async Task Cleanup()
    {
        _lifetime.Cancel();
        await Task.WhenAll(_pending);
        _lifetime.Dispose();
    }
}
