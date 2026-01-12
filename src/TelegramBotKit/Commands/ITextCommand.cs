using Telegram.Bot.Types;

namespace TelegramBotKit.Commands;

/// <summary>
/// Обработчик текста (например "hi", "меню", "помощь").
/// </summary>
public interface ITextCommand : ICommand
{
    /// <summary>
    /// Триггеры (ключевые слова/фразы).
    /// </summary>
    IReadOnlyCollection<string> Triggers { get; }

    /// <summary>
    /// Игнорировать регистр при сравнении.
    /// </summary>
    bool IgnoreCase => true;

    Task HandleAsync(Message message, BotContext ctx, CancellationToken ct);
}