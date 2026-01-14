namespace TelegramBotKit.Commands;

/// <summary>
/// Marks a command.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class CommandAttribute : Attribute
{
}
