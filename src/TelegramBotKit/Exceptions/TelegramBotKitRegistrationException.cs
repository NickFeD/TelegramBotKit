namespace TelegramBotKit;

/// <summary>
/// Thrown when registering handlers/commands results in an invalid configuration (duplicates, missing services, etc.).
/// </summary>
public sealed class TelegramBotKitRegistrationException : TelegramBotKitException
{
    /// <summary>
    /// Creates a new <see cref="TelegramBotKitRegistrationException"/>.
    /// </summary>
    /// <param name="message">Human-readable error message.</param>
    public TelegramBotKitRegistrationException(string message) : base(message) { }

    /// <summary>
    /// Creates a new <see cref="TelegramBotKitRegistrationException"/>.
    /// </summary>
    /// <param name="message">Human-readable error message.</param>
    /// <param name="inner">Inner exception.</param>
    public TelegramBotKitRegistrationException(string message, Exception inner) : base(message, inner) { }
}
