namespace TelegramBotKit.Messaging;

public sealed class QueuedMessageSenderOptions
{
    /// <summary>
    /// Gets or sets the maximum queue size.
    /// </summary>
    public int MaxQueueSize { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the global maximum messages per second.
    /// </summary>
    public int GlobalMaxPerSecond { get; set; } = 25;

    /// <summary>
    /// Gets or sets the minimum delay per chat.
    /// </summary>
    public TimeSpan PerChatMinDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or sets the maximum retry attempts.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 5;

    /// <summary>
    /// Gets or sets the default retry delay.
    /// </summary>
    public TimeSpan DefaultRetryDelay { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Gets or sets the base delay for server errors.
    /// </summary>
    public TimeSpan ServerErrorBaseDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or sets the maximum delay for server errors.
    /// </summary>
    public TimeSpan ServerErrorMaxDelay { get; set; } = TimeSpan.FromSeconds(20);
}
