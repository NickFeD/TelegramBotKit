using Telegram.Bot.Types;

namespace TelegramBotKit.Fallbacks;

public interface IDefaultMessageHandler
{
    Task HandleAsync(Message message, BotContext ctx);
}
