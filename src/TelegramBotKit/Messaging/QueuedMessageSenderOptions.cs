namespace TelegramBotKit.Messaging;

/// <summary>
/// Configures the in-memory outgoing queue, request pacing and retry limits.
/// </summary>
public sealed class QueuedMessageSenderOptions
{
    /// <summary>
    /// Gets or sets the positive number of pending operations the channel can hold. Defaults to 1000.
    /// </summary>
    /// <remarks>When the channel is full, admission waits for capacity. This does not bound waiting callers.</remarks>
    public int MaxQueueSize { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the global request-pacing rate. Defaults to 25 per second; nonpositive values disable it.
    /// </summary>
    public int GlobalMaxPerSecond { get; set; } = 25;

    /// <summary>
    /// Gets or sets the minimum interval between requests to the same chat. Defaults to one second.
    /// </summary>
    /// <remarks>Nonpositive values disable per-chat pacing. Callback acknowledgments have no chat key.</remarks>
    public TimeSpan PerChatMinDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or sets the maximum total attempts, including the initial request. Defaults to five.
    /// </summary>
    /// <remarks>At least one attempt is made even when this value is nonpositive.</remarks>
    public int MaxRetryAttempts { get; set; } = 5;

    /// <summary>
    /// Gets or sets the fallback delay for HTTP 429 responses without a positive retry-after value. Defaults to two seconds.
    /// </summary>
    public TimeSpan DefaultRetryDelay { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Gets or sets the exponential-backoff base for HTTP 5xx and transport errors. Defaults to one second.
    /// </summary>
    public TimeSpan ServerErrorBaseDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or sets the upper bound for exponential backoff. Defaults to twenty seconds.
    /// </summary>
    public TimeSpan ServerErrorMaxDelay { get; set; } = TimeSpan.FromSeconds(20);
}
