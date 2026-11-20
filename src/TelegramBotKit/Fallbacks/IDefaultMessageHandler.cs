using Telegram.Bot.Types;

namespace TelegramBotKit.Fallbacks;

/// <summary>
/// Handles messages that match neither a built-in conversation wait nor a registered command.
/// </summary>
public interface IDefaultMessageHandler
{
    /// <summary>
    /// Processes a message left unmatched by the built-in message route.
    /// </summary>
    /// <param name="message">The Telegram message to process or use as the operation's target.</param>
    /// <param name="ctx">The current update context, including scoped services and cancellation.</param>
    /// <returns>The asynchronous operation, which completes after processing finishes.</returns>
    Task HandleAsync(Message message, BotContext ctx);
}
