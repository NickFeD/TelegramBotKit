namespace TelegramBotKit;

/// <summary>
/// Thrown when TelegramBotKit is misconfigured (invalid options, missing required settings, etc.).
/// </summary>
public sealed class TelegramBotKitConfigurationException : TelegramBotKitException
{
    /// <summary>
    /// Creates a new <see cref="TelegramBotKitConfigurationException"/>.
    /// </summary>
    /// <param name="message">Human-readable error message.</param>
    public TelegramBotKitConfigurationException(string message) : base(message) { }

    /// <summary>
    /// Creates a new <see cref="TelegramBotKitConfigurationException"/>.
    /// </summary>
    /// <param name="message">Human-readable error message.</param>
    /// <param name="inner">Inner exception.</param>
    public TelegramBotKitConfigurationException(string message, Exception inner) : base(message, inner) { }
}
