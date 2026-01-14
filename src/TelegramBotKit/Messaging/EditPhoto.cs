using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramBotKit.Messaging;

public sealed record EditPhoto
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
    /// Gets or sets the reply markup.
    /// </summary>
    public InlineKeyboardMarkup? ReplyMarkup { get; init; }
}
