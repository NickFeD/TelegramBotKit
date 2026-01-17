namespace TelegramBotKit;

/// <summary>
/// Thrown when callback_data is malformed or cannot be parsed.
/// </summary>
public sealed class TelegramBotKitCallbackDataException : TelegramBotKitException
{
    /// <summary>
    /// Creates a new <see cref="TelegramBotKitCallbackDataException"/>.
    /// </summary>
    /// <param name="message">Human-readable error message.</param>
    public TelegramBotKitCallbackDataException(string message) : base(message) { }

    /// <summary>
    /// Creates a new <see cref="TelegramBotKitCallbackDataException"/>.
    /// </summary>
    /// <param name="message">Human-readable error message.</param>
    /// <param name="inner">Inner exception.</param>
    public TelegramBotKitCallbackDataException(string message, Exception inner) : base(message, inner) { }
}
