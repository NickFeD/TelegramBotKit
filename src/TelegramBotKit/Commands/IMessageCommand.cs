using Telegram.Bot.Types;

namespace TelegramBotKit.Commands;

public interface IMessageCommand : ICommand
{
    Task HandleAsync(Message message, BotContext ctx);
}
