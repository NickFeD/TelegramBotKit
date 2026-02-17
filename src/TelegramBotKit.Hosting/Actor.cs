using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Telegram.Bot.Types;

namespace TelegramBotKit.Hosting;

internal sealed class Actor
{
    private readonly ActorKey _key;
    private readonly UpdateActorScheduler _owner;
    private readonly Channel<UpdateWorkItem> _queue;

    private int _pending;
    private long _lastActivityTicks;

    public Actor(ActorKey key, UpdateActorScheduler owner)
    {
        _key = key;
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

    public ValueTask EnqueueAsync(Update upd, CancellationToken ct)
    {
        Interlocked.Increment(ref _pending);
        Touch();

        return WriteAsync(new UpdateWorkItem(upd, ct));

        async ValueTask WriteAsync(UpdateWorkItem item)
        {
            try
            {
                await _queue.Writer.WriteAsync(item, ct).ConfigureAwait(false);
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
                    () => _owner.Dispatcher.DispatchAsync(item.Update, item.Ct),
                    item.Ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _owner.Log.LogError(
                    ex,
                    "Actor update processing failed (kind={Kind}, id={Id}), updateId={UpdateId}",
                    _key.Kind,
                    _key.Id,
                    item.Update.Id);
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
