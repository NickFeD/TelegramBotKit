namespace TelegramBotKit.Tests;

// A per-test clock: timer callbacks are explicitly driven, never backed by wall-clock delays.
internal sealed class TestTimeProvider : TimeProvider
{
    private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly object _gate = new();
    private readonly List<TestTimer> _timers = new();
    public override DateTimeOffset GetUtcNow() { lock (_gate) return _now; }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new TestTimer(this, callback, state);
        timer.Change(dueTime, period);
        lock (_gate) _timers.Add(timer);
        return timer;
    }

    internal void Advance(TimeSpan elapsed)
    {
        TestTimer[] timers;
        lock (_gate) { _now += elapsed; timers = _timers.ToArray(); }
        foreach (var timer in timers) timer.FireIfDue();
    }

    private sealed class TestTimer(TestTimeProvider clock, TimerCallback callback, object? state) : ITimer
    {
        private DateTimeOffset _due;
        private TimeSpan _period;
        private volatile bool _disposed;

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            if (_disposed) return false;
            _due = dueTime == Timeout.InfiniteTimeSpan ? DateTimeOffset.MaxValue : clock.GetUtcNow() + dueTime;
            _period = period;
            return true;
        }

        internal void FireIfDue()
        {
            if (_disposed || clock.GetUtcNow() < _due) return;
            _due = _period == Timeout.InfiniteTimeSpan ? DateTimeOffset.MaxValue : clock.GetUtcNow() + _period;
            callback(state);
        }

        public void Dispose() => _disposed = true;
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}
