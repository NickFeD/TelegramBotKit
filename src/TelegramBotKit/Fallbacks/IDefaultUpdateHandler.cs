namespace TelegramBotKit.Fallbacks;

public interface IDefaultUpdateHandler
{
    Task HandleAsync(BotContext ctx);
}
