namespace TelegramBotKit.Options;

public sealed class TelegramBotKitOptions
{
    /// <summary>
    /// Gets or sets the bot token.
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the polling options.
    /// </summary>
    public PollingOptions Polling { get; set; } = new();
}
