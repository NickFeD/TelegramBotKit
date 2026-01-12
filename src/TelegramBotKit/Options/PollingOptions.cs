using Telegram.Bot.Types.Enums;

namespace TelegramBotKit.Options;

public sealed class PollingOptions
{
    /// <summary>
    /// GetUpdates limit (1..100). Обычно 100.
    /// </summary>
    public int Limit { get; set; } = 100;

    /// <summary>
    /// GetUpdates timeout (секунды). Обычно 10.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Какие типы апдейтов принимать. Пустой массив означает "все" (дефолт Telegram).
    /// </summary>
    public UpdateType[] AllowedUpdates { get; set; } = Array.Empty<UpdateType>();
}
