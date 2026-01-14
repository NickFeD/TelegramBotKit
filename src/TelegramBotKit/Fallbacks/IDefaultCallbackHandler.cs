using Telegram.Bot.Types;

namespace TelegramBotKit.Fallbacks;

public interface IDefaultCallbackHandler
{
    Task HandleAsync(CallbackQuery query, BotContext ctx);
}
