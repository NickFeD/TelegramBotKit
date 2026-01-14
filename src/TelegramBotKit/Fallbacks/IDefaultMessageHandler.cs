using Telegram.Bot.Types;

namespace TelegramBotKit.Fallbacks;

/// <summary>
/// Defines the contract for a default message handler.
/// </summary>
public interface IDefaultMessageHandler
{
    /// <summary>
    /// Handles the update asynchronously.
    /// </summary>
    Task HandleAsync(Message message, BotContext ctx);
}
