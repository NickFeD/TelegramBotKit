using Telegram.Bot.Types;

namespace TelegramBotKit.Dispatching;

/// <summary>
/// Defines the terminal that takes final ownership of a typed update route after its
/// route-local middleware has completed.
/// </summary>
/// <typeparam name="TPayload">The non-null payload selected by the route descriptor.</typeparam>
public interface IUpdatePayloadHandler<TPayload> where TPayload : class
{
    /// <summary>
    /// Handles the selected payload as the route's single terminal operation.
    /// </summary>
    /// <param name="payload">The non-null payload extracted from the Telegram update.</param>
    /// <param name="ctx">The update-wide context for this dispatch.</param>
    /// <returns>A task that represents terminal processing.</returns>
    Task HandleAsync(TPayload payload, BotContext ctx);
}

