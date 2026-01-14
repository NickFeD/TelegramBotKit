using Telegram.Bot.Types;

namespace TelegramBotKit.Commands;

/// <summary>
/// Обработчик текста (например "hi", "меню", "помощь").
/// </summary>
public interface ITextCommand : ICommand
{
    Task HandleAsync(Message message, BotContext ctx);
}