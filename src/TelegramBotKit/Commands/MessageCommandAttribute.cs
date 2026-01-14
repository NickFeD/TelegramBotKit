namespace TelegramBotKit.Commands;

/// <summary>
/// Метаданные для slash-команды (сообщение вида /start, /help и т.д.).
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class MessageCommandAttribute : Attribute
{
    public MessageCommandAttribute(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            throw new ArgumentException("Command must not be empty.", nameof(command));

        Command = command;
    }

    public string Command { get; }
}
