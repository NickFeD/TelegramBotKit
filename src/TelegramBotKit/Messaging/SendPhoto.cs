using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramBotKit.Messaging;

public sealed record SendPhoto
{
    public required InputFile Photo { get; init; }

    public string? Caption { get; init; }

    public ParseMode ParseMode { get; init; } = ParseMode.None;

    public IEnumerable<MessageEntity>? CaptionEntities { get; init; }

    public ReplyMarkup? ReplyMarkup { get; init; }

    public bool DisableNotification { get; init; }

    public bool ProtectContent { get; init; }

    public bool HasSpoiler { get; init; }
}
