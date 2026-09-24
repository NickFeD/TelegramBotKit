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

        _ = Task.Run(RunAsync, CancellationToken.None);
    }

    /// <summary>
    /// Gets the pending count.
    /// </summary>
    public int PendingCount => Volatile.Read(ref _pending);

    public DateTime LastActivityUtc
    {
        get
        {
            var ticks = Volatile.Read(ref _lastActivityTicks);
            return ticks == 0 ? DateTime.UtcNow : new DateTime(ticks, DateTimeKind.Utc);
        }
    }

    public ValueTask EnqueueAsync(UpdateWorkItem item)
    {
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
            try
            {
                Touch();
                await _owner.RunWithGlobalLimitAsync(
                    item.Execute,
                    item.Ct).ConfigureAwait(false);
                item.Completion.TrySetResult();
            }
            catch (OperationCanceledException ex)
            {
                item.Completion.TrySetCanceled(ex.CancellationToken);
            }
            catch (Exception ex)
            {
                // The scheduler observes completion and logs once for either lane.
                item.Completion.TrySetException(ex);
            }
            finally
            {
                Interlocked.Decrement(ref _pending);
                Touch();
            }
        }
    }

    private void Touch()
        => Volatile.Write(ref _lastActivityTicks, DateTime.UtcNow.Ticks);
}
