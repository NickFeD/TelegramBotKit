namespace TelegramBotKit;

/// <summary>
/// Thrown when an update cannot be dispatched to a handler.
/// </summary>
public sealed class TelegramBotKitDispatchException : TelegramBotKitException
{
    /// <summary>
    /// Creates a new <see cref="TelegramBotKitDispatchException"/>.
    /// </summary>
    /// <param name="message">Human-readable error message.</param>
    public TelegramBotKitDispatchException(string message) : base(message) { }

    /// <summary>
    /// Creates a new <see cref="TelegramBotKitDispatchException"/>.
    /// </summary>
    /// <param name="message">Human-readable error message.</param>
    /// <param name="inner">Inner exception.</param>
    public TelegramBotKitDispatchException(string message, Exception inner) : base(message, inner) { }
}
