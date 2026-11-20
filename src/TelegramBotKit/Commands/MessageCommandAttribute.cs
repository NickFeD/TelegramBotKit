namespace TelegramBotKit.Commands;

/// <summary>
/// Declares the slash-command key used by the bundled generator to register an <see cref="IMessageCommand"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class MessageCommandAttribute : Attribute
{
    /// <summary>
    /// Declares a slash-command key for the attributed handler.
    /// </summary>
    /// <param name="command">The slash-command key, with or without a leading slash.</param>
    public MessageCommandAttribute(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            throw new ArgumentException("Command must not be empty.", nameof(command));

        Command = command;
    }

    /// <summary>
    /// Gets the declared command key before registration normalizes its leading slash and bot suffix.
    /// </summary>
    public string Command { get; }
}
