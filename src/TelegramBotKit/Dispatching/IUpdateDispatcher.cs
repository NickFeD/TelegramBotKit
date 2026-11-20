using Telegram.Bot.Types;

namespace TelegramBotKit.Dispatching;

/// <summary>
/// Processes a Telegram update through middleware, its selected route and the update scope.
/// </summary>
public interface IUpdateDispatcher
{
    /// <summary>
    /// Processes an update and completes after its handler and scoped-service disposal finish.
    /// </summary>
    /// <remarks>
    /// Completion includes processing and disposal of the update scope. Implementations must
    /// observe cancellation and propagate failures to a direct caller. A polling host logs
    /// failures; it does not provide durable retries. For conversation-response scheduling,
    /// also implement <see cref="IScheduledUpdateDispatcher"/>.
    /// </remarks>
    /// <param name="update">The incoming Telegram update.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>The asynchronous operation, which completes after processing finishes.</returns>
    Task DispatchAsync(Update update, CancellationToken ct = default);
}
