namespace TelegramBotKit.Options;

/// <summary>
/// Как бот получает апдейты: через long polling или webhook.
/// </summary>
public enum UpdateDeliveryMode
{
    Polling = 0,
    Webhook = 1
}
