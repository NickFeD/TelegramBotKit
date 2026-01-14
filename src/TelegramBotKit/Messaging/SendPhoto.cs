using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramBotKit.Messaging;

/// <summary>
/// Provides a send photo.
/// </summary>
public sealed record SendPhoto
{
    /// <summary>
    /// Gets or sets the photo.
    /// </summary>
    public required InputFile Photo { get; init; }

    /// <summary>
    /// Gets or sets the caption.
    /// </summary>
    public string? Caption { get; init; }

    /// <summary>
    /// Gets or sets the parse mode.
    /// </summary>
    public ParseMode ParseMode { get; init; } = ParseMode.None;

    /// <summary>
    /// Gets or sets the caption entities.
    /// </summary>
    public IEnumerable<MessageEntity>? CaptionEntities { get; init; }

    /// <summary>
    /// Gets or sets the reply markup.
    /// </summary>
    public ReplyMarkup? ReplyMarkup { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether notifications are disabled.
    /// </summary>
    public bool DisableNotification { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether content is protected.
    /// </summary>
    public bool ProtectContent { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether spoiler mode is enabled.
    /// </summary>
    public bool HasSpoiler { get; init; }
}
