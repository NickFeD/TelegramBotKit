using Telegram.Bot.Types;

namespace TelegramBotKit.Commands;

/// <summary>
/// Обработчик команд вида /start, /help и т.д.
/// </summary>
public interface IMessageCommand : ICommand
{
    Task HandleAsync(Message message, BotContext ctx);
}