using Telegram.Bot.Types;

namespace TelegramBotKit.Dispatching;

public interface IUpdateDispatcher
{
    Task DispatchAsync(Update update, CancellationToken ct = default);
}
