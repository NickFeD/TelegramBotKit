using Telegram.Bot.Types;

namespace TelegramBotKit.Dispatching;

/// <summary>
/// Публичная точка входа для обработки Update.
/// Реализация скрыта внутри библиотеки.
/// </summary>
public interface IUpdateDispatcher
{
    Task DispatchAsync(Update update, CancellationToken ct = default);
}
