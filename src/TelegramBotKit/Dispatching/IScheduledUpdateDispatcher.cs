using Telegram.Bot.Types;

namespace TelegramBotKit.Dispatching;

/// <summary>
/// Optional dispatcher capability that separates update policy from execution ordering.
/// Implement this alongside <see cref="IUpdateDispatcher"/> to participate in scheduled delivery.
/// </summary>
/// <remarks>
/// Hosting calls this method instead of DispatchAsync when the capability is present.
/// A decorator must forward it to a capable inner dispatcher to retain conversation-response
/// scheduling. The returned task must cover the entire update lifetime, including scope disposal.
/// </remarks>
public interface IScheduledUpdateDispatcher
{
    /// <summary>Dispatches an update using the host's scheduling continuation when ordering is required.</summary>
    /// <param name="update">The incoming update.</param>
    /// <param name="ct">Cancellation for the entire update lifetime.</param>
    /// <param name="schedule">
    /// Runs and awaits work under the host's actor and concurrency policy. Normal processing
    /// must use this continuation; an active conversation response may bypass it to release
    /// a waiting command. A stale response must schedule its remaining processing without
    /// repeating middleware. Pass the effective update cancellation token to the continuation.
    /// </param>
    /// <returns>A task completed only after processing, unwind and disposal have finished.</returns>
    Task DispatchScheduledAsync(Update update, CancellationToken ct,
        Func<Func<Task>, CancellationToken, Task> schedule);
}
