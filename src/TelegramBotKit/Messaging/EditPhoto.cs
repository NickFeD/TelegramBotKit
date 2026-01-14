using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramBotKit.Messaging;

public sealed record EditPhoto
{
    public required InputFile Photo { get; init; }

    public string? Caption { get; init; }
    public ParseMode ParseMode { get; init; } = ParseMode.None;

    public InlineKeyboardMarkup? ReplyMarkup { get; init; }
}
