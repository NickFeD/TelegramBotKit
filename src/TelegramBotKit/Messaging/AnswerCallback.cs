namespace TelegramBotKit.Messaging;

/// <summary>
/// Provides an answer callback.
/// </summary>
public sealed record AnswerCallback
{
    /// <summary>
    /// Gets or sets the text.
    /// </summary>
    public string? Text { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether show alert is enabled.
    /// </summary>
    public bool ShowAlert { get; init; } = false;

    /// <summary>
    /// Gets or sets the url.
    /// </summary>
    public string? Url { get; init; }
}
