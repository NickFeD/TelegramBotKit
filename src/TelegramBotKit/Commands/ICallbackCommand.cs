using Telegram.Bot.Types;

namespace TelegramBotKit.Commands;

public interface ICallbackCommand : ICommand
{
    Task HandleAsync(CallbackQuery query, string[] args, BotContext ctx);
}
