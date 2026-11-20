using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramBotKit.Messaging;

/// <summary>
/// Specifies replacement text, formatting and an optional inline keyboard for an existing message.
/// </summary>
public sealed record EditText
{
    /// <summary>
    /// Gets the text supplied with this request.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Gets the text or caption parse mode. Defaults to no markup parsing.
    /// </summary>
    public ParseMode ParseMode { get; init; } = ParseMode.None;

    /// <summary>
    /// Gets optional link-preview settings for the message text.
    /// </summary>
    public LinkPreviewOptions? LinkPreviewOptions { get; init; }

    /// <summary>
    /// Gets explicit formatting entities for the message text.
    /// </summary>
    public IEnumerable<MessageEntity>? Entities { get; init; }

    /// <summary>
    /// Gets the optional keyboard markup sent with this request.
    /// </summary>
    public InlineKeyboardMarkup? ReplyMarkup { get; init; }
}
