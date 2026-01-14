using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramBotKit.Messaging;

/// <summary>
/// Provides a send text.
/// </summary>
public sealed record SendText
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
    public ReplyMarkup? ReplyMarkup { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether notifications are disabled.
    /// </summary>
    public bool DisableNotification { get; init; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether content is protected.
    /// </summary>
    public bool ProtectContent { get; init; } = false;
}
