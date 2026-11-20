namespace TelegramBotKit.Messaging;

/// <summary>
/// Specifies the notification, alert or URL used to acknowledge a callback query.
/// </summary>
public sealed record AnswerCallback
{
    /// <summary>
    /// Gets the text supplied with this request.
    /// </summary>
    public string? Text { get; init; }

    /// <summary>
    /// Gets whether the notification is displayed as an alert instead of a toast.
    /// </summary>
    public bool ShowAlert { get; init; } = false;

    /// <summary>
    /// Gets the optional URL included in the callback acknowledgment.
    /// </summary>
    public string? Url { get; init; }
}
