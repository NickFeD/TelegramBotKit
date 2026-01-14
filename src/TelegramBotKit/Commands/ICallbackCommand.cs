using Telegram.Bot.Types;

namespace TelegramBotKit.Commands;

/// <summary>
/// Обработчик callback query. Key — первый токен в callback_data.
/// Пример callback_data: "like 123" => Key="like", args=["123"].
/// </summary>
public interface ICallbackCommand : ICommand
{
    Task HandleAsync(CallbackQuery query, string[] args, BotContext ctx);
}