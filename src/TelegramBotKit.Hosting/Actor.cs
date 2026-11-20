using System.Threading.Channels;

namespace TelegramBotKit.Hosting;

internal sealed class Actor
{
    private readonly UpdateActorScheduler _owner;
    private readonly Channel<UpdateWorkItem> _queue;

    private int _pending;
    private long _lastActivityTicks;

    public Actor(UpdateActorScheduler owner)
    {
        _owner = owner;
        _queue = Channel.CreateUnbounded<UpdateWorkItem>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        Touch();

        Completion = Task.Run(RunAsync, CancellationToken.None);
    }

    internal Task Completion { get; }

    /// <summary>
    /// Gets the number of admitted updates not yet completed, including the currently executing update.
    /// </summary>
    public int PendingCount => Volatile.Read(ref _pending);

    public DateTime LastActivityUtc
    {
        get
        {
            var ticks = Volatile.Read(ref _lastActivityTicks);
            return ticks == 0 ? _owner.UtcNow : new DateTime(ticks, DateTimeKind.Utc);
        }
    }

    public ValueTask EnqueueAsync(UpdateWorkItem item)
    {
        item.Ct.ThrowIfCancellationRequested();
        Interlocked.Increment(ref _pending);
        Touch();

        return WriteAsync(item);

        async ValueTask WriteAsync(UpdateWorkItem item)
        {
            try
            {
                await _queue.Writer.WriteAsync(item, item.Ct).ConfigureAwait(false);
            }
            catch
            {
                Interlocked.Decrement(ref _pending);
                Touch();
                throw;
            }
        }
    }

    public void Complete() => _queue.Writer.TryComplete();

    private async Task RunAsync()
    {
        await foreach (var item in _queue.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            Exception? error = null;
            try
            {
                Touch();
                await _owner.RunWithGlobalLimitAsync(
                    item.Execute,
                    item.Ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // The scheduler observes completion and logs once for either lane.
                error = ex;
            }
            finally
            {
                Interlocked.Decrement(ref _pending);
                Touch();
            }
            if (error is OperationCanceledException canceled)
                item.Completion.TrySetCanceled(canceled.CancellationToken);
            else if (error is not null)
                item.Completion.TrySetException(error);
            else
                item.Completion.TrySetResult();
        }
    }

    private void Touch()
        => Volatile.Write(ref _lastActivityTicks, _owner.UtcNow.Ticks);
}
