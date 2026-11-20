using Telegram.Bot.Types.Enums;

namespace TelegramBotKit.Options;

/// <summary>
/// Configures GetUpdates requests and the legacy shared handler-concurrency limit.
/// </summary>
public sealed class PollingOptions
{
    /// <summary>
    /// Gets or sets the legacy nonnegative global handler-concurrency limit; zero means unlimited.
    /// </summary>
    /// <remarks>Shared Hosting UpdateProcessingOptions can override this value for either transport.</remarks>
    public int MaxDegreeOfParallelism { get; set; } = 0;

    /// <summary>
    /// Gets or sets the GetUpdates batch limit, from one to one hundred. Defaults to one hundred.
    /// </summary>
    public int Limit { get; set; } = 100;

    /// <summary>
    /// Gets or sets the GetUpdates timeout, from zero to sixty seconds. Zero requests short polling; the default is ten.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Gets or sets update types requested from Telegram; an empty array uses Telegram's default subscription.
    /// </summary>
    public UpdateType[] AllowedUpdates { get; set; } = Array.Empty<UpdateType>();
}
