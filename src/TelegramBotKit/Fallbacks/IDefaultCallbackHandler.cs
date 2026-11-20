using Telegram.Bot.Types;

namespace TelegramBotKit.Fallbacks;

/// <summary>
/// Handles callback queries whose data does not match a registered command key.
/// </summary>
public interface IDefaultCallbackHandler
{
    /// <summary>
    /// Processes a callback query left unmatched by the built-in callback route.
    /// </summary>
    /// <param name="query">The incoming callback query.</param>
    /// <param name="ctx">The current update context, including scoped services and cancellation.</param>
    /// <returns>The asynchronous operation, which completes after processing finishes.</returns>
    Task HandleAsync(CallbackQuery query, BotContext ctx);
}
