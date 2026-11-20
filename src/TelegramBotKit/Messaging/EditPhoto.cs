using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramBotKit.Messaging;

/// <summary>
/// Specifies a replacement photo, caption and optional inline keyboard for an existing message.
/// </summary>
public sealed record EditPhoto
{
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
    /// Gets the optional keyboard markup sent with this request.
    /// </summary>
    public InlineKeyboardMarkup? ReplyMarkup { get; init; }
}
