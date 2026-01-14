namespace TelegramBotKit.Commands;

/// <summary>
/// Marks a message command.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class MessageCommandAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of MessageCommandAttribute.
    /// </summary>
    public MessageCommandAttribute(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            throw new ArgumentException("Command must not be empty.", nameof(command));

        Command = command;
    }

    /// <summary>
    /// Gets the command.
    /// </summary>
    public string Command { get; }
}
