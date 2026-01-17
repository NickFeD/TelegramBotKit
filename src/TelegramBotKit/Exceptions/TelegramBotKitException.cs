namespace TelegramBotKit;

/// <summary>
/// Base exception type for TelegramBotKit.
/// </summary>
public abstract class TelegramBotKitException : Exception
{
    /// <summary>
    /// Creates a new <see cref="TelegramBotKitException"/>.
    /// </summary>
    /// <param name="message">Human-readable error message.</param>
    protected TelegramBotKitException(string message) : base(message) { }

    /// <summary>
    /// Creates a new <see cref="TelegramBotKitException"/>.
    /// </summary>
    /// <param name="message">Human-readable error message.</param>
    /// <param name="inner">Inner exception.</param>
    protected TelegramBotKitException(string message, Exception inner) : base(message, inner) { }
}
