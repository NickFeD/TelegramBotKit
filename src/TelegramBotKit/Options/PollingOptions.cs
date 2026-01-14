using Telegram.Bot.Types.Enums;

namespace TelegramBotKit.Options;

public sealed class PollingOptions
{
    /// <summary>
    /// Gets or sets the maximum degree of parallelism.
    /// </summary>
    public int MaxDegreeOfParallelism { get; set; } = 0;

    /// <summary>
    /// Gets or sets the update limit.
    /// </summary>
    public int Limit { get; set; } = 100;

    /// <summary>
    /// Gets or sets the polling timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Gets or sets the allowed update types.
    /// </summary>
    public UpdateType[] AllowedUpdates { get; set; } = Array.Empty<UpdateType>();
}
