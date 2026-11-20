namespace TelegramBotKit.Options;

/// <summary>
/// Specifies update delivery mode.
/// </summary>
public enum UpdateDeliveryMode
{
    /// <summary>
    /// Receives updates through Telegram GetUpdates using the Polling adapter.
    /// </summary>
    Polling = 0,
    /// <summary>
    /// Receives updates through an HTTP webhook adapter.
    /// </summary>
    Webhook = 1
}
