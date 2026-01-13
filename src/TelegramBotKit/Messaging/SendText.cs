using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramBotKit.Messaging;

/// <summary>
/// DTO для отправки текстового сообщения. Можно переиспользовать как шаблон:
/// var tpl = new SendText { ParseMode = ParseMode.Html, LinkPreviewOptions = new(false) };
/// await sender.SendText(chatId, tpl with { Text = "Hello" }, ct);
/// </summary>
public sealed record SendText
{
    /// <summary>Текст сообщения.</summary>
    public required string Text { get; init; }

    /// <summary>Режим разметки (Html/Markdown/None).</summary>
    public ParseMode ParseMode { get; init; } = ParseMode.None;

    /// <summary>Настройки предпросмотра ссылок (в v21+ заменяет disableWebPagePreview).</summary>
    public LinkPreviewOptions? LinkPreviewOptions { get; init; }

    /// <summary>Сущности текста (если ты сам формируешь entities).</summary>
    public IEnumerable<MessageEntity>? Entities { get; init; }

    /// <summary>Клавиатура (Inline/Reply/RemoveKeyboard/ForceReply).</summary>
    public ReplyMarkup? ReplyMarkup { get; init; }

    /// <summary>Отключить уведомление (тихая отправка).</summary>
    public bool DisableNotification { get; init; } = false;

    /// <summary>Защитить контент (нельзя пересылать/сохранять).</summary>
    public bool ProtectContent { get; init; } = false;
}
