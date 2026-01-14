using Telegram.Bot.Types;

namespace TelegramBotKit.Fallbacks;

/// <summary>
/// Вызывается, когда callback_data не матчится ни под один ICallbackCommand (ключ неизвестен).
/// </summary>
public interface IDefaultCallbackHandler
{
    Task HandleAsync(CallbackQuery query, BotContext ctx);
}
