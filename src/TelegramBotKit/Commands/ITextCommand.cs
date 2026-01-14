using Telegram.Bot.Types;

namespace TelegramBotKit.Commands;

public interface ITextCommand : ICommand
{
    Task HandleAsync(Message message, BotContext ctx);
}
