using Telegram.Bot.Types;

namespace TelegramBotKit.Dispatching;

public interface IUpdatePayloadHandler<TPayload> where TPayload : class
{
    Task HandleAsync(TPayload payload, BotContext ctx);
}

