using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramBotKit.Messaging;

public sealed record EditText
{
    /// <summary>
    /// Gets or sets the text.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Gets or sets the parse mode.
    /// </summary>
    public ParseMode ParseMode { get; init; } = ParseMode.None;

    /// <summary>
    /// Gets or sets the link preview options.
    /// </summary>
    public LinkPreviewOptions? LinkPreviewOptions { get; init; }

    /// <summary>
    /// Gets or sets the message entities.
    /// </summary>
    public IEnumerable<MessageEntity>? Entities { get; init; }

    /// <summary>
    /// Gets or sets the reply markup.
    /// </summary>
    public InlineKeyboardMarkup? ReplyMarkup { get; init; }
}
