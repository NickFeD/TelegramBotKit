using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramBotKit.Messaging;

/// <summary>
/// Specifies a photo, optional caption, keyboard and delivery options for a new message.
/// </summary>
public sealed record SendPhoto
{
    /// <summary>
    /// Gets the destination forum topic. When null, message-based sends and replies
    /// inherit the source message's thread; explicit chat sends omit the thread.
    /// </summary>
    public int? MessageThreadId { get; init; }

    /// <summary>
    /// Gets the photo file, stream, URL or Telegram file identifier supplied with this request.
    /// </summary>
    public required InputFile Photo { get; init; }

    /// <summary>
    /// Gets the optional photo caption.
    /// </summary>
    public string? Caption { get; init; }

    /// <summary>
    /// Gets the text or caption parse mode. Defaults to no markup parsing.
    /// </summary>
    public ParseMode ParseMode { get; init; } = ParseMode.None;

    /// <summary>
    /// Gets explicit formatting entities for the photo caption.
    /// </summary>
    public IEnumerable<MessageEntity>? CaptionEntities { get; init; }

    /// <summary>
    /// Gets the optional keyboard markup sent with this request.
    /// </summary>
    public ReplyMarkup? ReplyMarkup { get; init; }

    /// <summary>
    /// Gets the silent-delivery flag for the new message.
    /// </summary>
    public bool DisableNotification { get; init; }

    /// <summary>
    /// Gets the content-protection flag for the new message.
    /// </summary>
    public bool ProtectContent { get; init; }

    /// <summary>
    /// Gets whether the photo is displayed behind a spoiler overlay.
    /// </summary>
    public bool HasSpoiler { get; init; }
}
