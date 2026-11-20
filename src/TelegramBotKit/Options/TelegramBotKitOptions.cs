namespace TelegramBotKit.Options;

/// <summary>
/// Configures the Telegram client token and polling defaults used by the delivery adapters.
/// </summary>
public sealed class TelegramBotKitOptions
{
    /// <summary>
    /// Gets or sets the nonempty bot token used to create the Telegram client.
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets polling settings, including the legacy handler-concurrency limit.
    /// </summary>
    public PollingOptions Polling { get; set; } = new();
}
