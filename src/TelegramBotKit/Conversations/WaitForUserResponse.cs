using System.Collections.Concurrent;
using Telegram.Bot.Types;

namespace TelegramBotKit.Conversations;

/// <summary>
/// Matches the next published message to an in-memory wait identified by chat and user.
/// </summary>
public sealed class WaitForUserResponse
{
    private readonly ConcurrentDictionary<WaitKey, TaskCompletionSource<Message>> _waiters = new();
    private readonly TimeProvider _timeProvider;

    /// <summary>Creates an in-memory response waiter using the system clock.</summary>
    public WaitForUserResponse() : this(TimeProvider.System) { }

    internal WaitForUserResponse(TimeProvider timeProvider) =>
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    // Scheduling hint only. The dispatcher must recheck delivery after middleware:
    // the waiter can time out or be consumed before then.
    internal bool IsWaitingFor(Message message) =>
        _waiters.ContainsKey(new WaitKey(message.Chat.Id, message.From?.Id ?? 0));

    /// <summary>
    /// Waits for the next message published for the specified chat and user.
    /// </summary>
    /// <remarks>
    /// Only one active wait is allowed per chat/user pair. Publication and cancellation
    /// atomically compete for the registration; a response can be accepted once.
    /// Timeout and cancellation return null. State is in memory and is not restored after restart.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">A chat or user identifier is zero, or the timeout is nonpositive.</exception>
    /// <exception cref="InvalidOperationException">Another wait is already active for the same chat and user.</exception>
    /// <param name="chatId">The identifier of the chat in which the response is expected.</param>
    /// <param name="userId">The identifier of the user whose next message is awaited.</param>
    /// <param name="timeout">The positive duration after which the wait returns null.</param>
    /// <param name="ct">Cancels the wait; cancellation returns null.</param>
    /// <returns>The accepted response, or null when the wait times out or is canceled.</returns>
    public async Task<Message?> WaitAsync(
        long chatId,
        long userId,
        TimeSpan timeout,
        CancellationToken ct = default)
    {
        if (chatId == 0) throw new ArgumentOutOfRangeException(nameof(chatId));
        if (userId == 0) throw new ArgumentOutOfRangeException(nameof(userId));
        if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));
        if (ct.IsCancellationRequested) return null;

        var key = new WaitKey(chatId, userId);

        var waiter = new TaskCompletionSource<Message>(TaskCreationOptions.RunContinuationsAsynchronously);

        if (!_waiters.TryAdd(key, waiter))
            throw new InvalidOperationException($"Already waiting for message from chat:{chatId} user:{userId}");

        try
        {
            using var timeoutSource = new CancellationTokenSource(timeout, _timeProvider);
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutSource.Token);
            using var cancellation = timeoutCts.Token.Register(() =>
            {
                // Cancellation and publication compete to claim this exact registration.
                if (RemoveOwnWaiter(key, waiter))
                    waiter.TrySetCanceled(timeoutCts.Token);
            });
            return await waiter.Task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        finally
        {
            RemoveOwnWaiter(key, waiter);
        }
    }

    /// <summary>
    /// Offers a message to the active wait for its chat and sender, consuming that wait at most once.
    /// </summary>
    /// <param name="message">The message offered to the wait matching its chat and sender.</param>
    /// <returns>True if the message completed an active wait; false if no matching wait accepted it.</returns>
    public bool TryPublish(Message message)
    {
        if (message is null) return false;

        var chatId = message.Chat.Id;
        var userId = message.From?.Id ?? 0;

        if (chatId == 0 || userId == 0)
            return false;

        var key = new WaitKey(chatId, userId);

        // Removal claims the response once, before another publisher can see the waiter.
        if (!_waiters.TryRemove(key, out var waiter))
            return false;
        return waiter.TrySetResult(message);
    }

    private bool RemoveOwnWaiter(WaitKey key, TaskCompletionSource<Message> waiter) =>
        _waiters.TryRemove(new KeyValuePair<WaitKey, TaskCompletionSource<Message>>(key, waiter));

    private readonly record struct WaitKey(long ChatId, long UserId);
}
