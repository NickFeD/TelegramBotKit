using Telegram.Bot.Types;

namespace TelegramBotKit.Fallbacks;

/// <summary>
/// Defines the contract for a default callback handler.
/// </summary>
public interface IDefaultCallbackHandler
{
    /// <summary>
    /// Handles the update asynchronously.
    /// </summary>
    Task HandleAsync(CallbackQuery query, BotContext ctx);
}
