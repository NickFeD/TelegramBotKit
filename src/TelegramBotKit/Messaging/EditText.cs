using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramBotKit.Messaging;

/// <summary>
/// DTO для редактирования текста сообщения.
/// </summary>
public sealed record EditText
{
    /// <summary>Новый текст.</summary>
    public required string Text { get; init; }

    /// <summary>Режим разметки (Html/Markdown/None).</summary>
    public ParseMode ParseMode { get; init; } = ParseMode.None;

    /// <summary>Настройки предпросмотра ссылок.</summary>
    public LinkPreviewOptions? LinkPreviewOptions { get; init; }

    /// <summary>Сущности текста (если ты сам формируешь entities).</summary>
    public IEnumerable<MessageEntity>? Entities { get; init; }

    /// <summary>Inline клавиатура (для edit обычно актуальна именно Inline).</summary>
    public InlineKeyboardMarkup? ReplyMarkup { get; init; }
}
