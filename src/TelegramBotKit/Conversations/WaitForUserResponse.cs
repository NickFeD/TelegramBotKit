using System.Collections.Concurrent;
using System.Threading.Channels;
using Telegram.Bot.Types;

namespace TelegramBotKit.Conversations;

public sealed class WaitForUserResponse
{
    private readonly ConcurrentDictionary<WaitKey, Channel<Message>> _waiters = new();

    public async Task<Message?> WaitAsync(
        long chatId,
        long userId,
        TimeSpan timeout,
        CancellationToken ct = default)
    {
        if (chatId == 0) throw new ArgumentOutOfRangeException(nameof(chatId));
        if (userId == 0) throw new ArgumentOutOfRangeException(nameof(userId));
        if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));

        var key = new WaitKey(chatId, userId);

        var channel = Channel.CreateBounded<Message>(new BoundedChannelOptions(1)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });

        if (!_waiters.TryAdd(key, channel))
            throw new InvalidOperationException($"Already waiting for message from chat:{chatId} user:{userId}");

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);

        try
        {
            return await channel.Reader.ReadAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        finally
        {
            _waiters.TryRemove(key, out _);
            channel.Writer.TryComplete();
        }
    }

    public bool TryPublish(Message message)
    {
        if (message is null) return false;

        var chatId = message.Chat.Id;
        var userId = message.From?.Id ?? 0;

        if (chatId == 0 || userId == 0)
            return false;

        var key = new WaitKey(chatId, userId);

        if (!_waiters.TryGetValue(key, out var channel))
            return false;

        if (channel.Writer.TryWrite(message))
        {
            _waiters.TryRemove(key, out _);
            channel.Writer.TryComplete();
            return true;
        }

        return false;
    }

    private readonly record struct WaitKey(long ChatId, long UserId);
}
