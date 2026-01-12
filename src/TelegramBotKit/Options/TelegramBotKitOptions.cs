namespace TelegramBotKit.Options;

public sealed class TelegramBotKitOptions
{
    /// <summary>
    /// Токен бота (обязателен).
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Настройки polling (GetUpdates).
    /// </summary>
    public PollingOptions Polling { get; set; } = new();
}
