using Telegram.Bot.Types;

namespace TelegramBotKit.Commands;

/// <summary>
/// Обработчик callback query. Key — первый токен в callback_data.
/// Пример callback_data: "like 123" => Key="like", args=["123"].
/// </summary>
public interface ICallbackCommand : ICommand
{
    /// <summary>
    /// Ключ обработчика (первый токен callback_data).
    /// </summary>
    string Key { get; }

    Task HandleAsync(CallbackQuery query, string[] args, BotContext ctx, CancellationToken ct);
}