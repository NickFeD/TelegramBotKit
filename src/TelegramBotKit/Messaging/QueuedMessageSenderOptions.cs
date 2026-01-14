namespace TelegramBotKit.Messaging;

/// <summary>
/// Настройки очередного sender'а (защита от 429/5xx).
/// </summary>
public sealed class QueuedMessageSenderOptions
{
    /// <summary>
    /// Максимальный размер очереди.
    /// </summary>
    public int MaxQueueSize { get; set; } = 1000;

    /// <summary>
    /// Глобальный лимит отправки (сообщений/сек). 0 = без лимита.
    /// </summary>
    public int GlobalMaxPerSecond { get; set; } = 25;

    /// <summary>
    /// Минимальная пауза между запросами в один и тот же чат.
    /// </summary>
    public TimeSpan PerChatMinDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Максимальное число попыток (включая первую).
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 5;

    /// <summary>
    /// Если Telegram не прислал retry_after — ждём столько.
    /// </summary>
    public TimeSpan DefaultRetryDelay { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Базовая задержка для экспоненциального backoff при 5xx/сетевых ошибках.
    /// </summary>
    public TimeSpan ServerErrorBaseDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Максимальная задержка backoff.
    /// </summary>
    public TimeSpan ServerErrorMaxDelay { get; set; } = TimeSpan.FromSeconds(20);
}
