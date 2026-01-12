using Telegram.Bot.Types.Enums;

namespace TelegramBotKit.Options;

/// <summary>
/// Настройки long polling
/// </summary>
public sealed class PollingOptions
{
    public Telegram.Bot.Types.Enums.UpdateType[] AllowedUpdates { get; set; } = Array.Empty<Telegram.Bot.Types.Enums.UpdateType>();

    public int MaxDegreeOfParallelism { get; set; } = 4;

    public int Limit { get; set; } = 100;

    public int TimeoutSeconds { get; set; } = 10;
}
