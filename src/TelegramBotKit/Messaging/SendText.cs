using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramBotKit.Messaging;

/// <summary>
/// Specifies text, formatting, keyboard and delivery options for a new Telegram message.
/// </summary>
public sealed record SendText
{
    /// <summary>
    /// Gets the destination forum topic. When null, message-based sends and replies
    /// inherit the source message's thread; explicit chat sends omit the thread.
    /// </summary>
    public int? MessageThreadId { get; init; }

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
    public ReplyMarkup? ReplyMarkup { get; init; }

    /// <summary>
    /// Gets the silent-delivery flag for the new message.
    /// </summary>
    public bool DisableNotification { get; init; } = false;

    /// <summary>
    /// Gets the content-protection flag for the new message.
    /// </summary>
    public bool ProtectContent { get; init; } = false;
}
