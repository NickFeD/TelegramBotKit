namespace TelegramBotKit.Options;

/// <summary>
/// Общие настройки TelegramBotKit
/// </summary>
public sealed class TelegramBotKitOptions
{
    /// <summary>
    /// Токен бота (обязательно)
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Режим получения апдейтов: Polling или Webhook
    /// </summary>
    public UpdateDeliveryMode Mode { get; set; } = UpdateDeliveryMode.Polling;

    /// <summary>
    /// Настройки webhook (используются только если Mode = Webhook)
    /// </summary>
    public WebhookOptions Webhook { get; set; } = new();

    /// <summary>
    /// Настройки polling (используются только если Mode = Polling)
    /// </summary>
    public PollingOptions Polling { get; set; } = new();
}