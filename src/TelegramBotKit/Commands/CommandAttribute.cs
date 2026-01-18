namespace TelegramBotKit.Commands;

/// <summary>
/// Marks a command.
/// </summary>
// This attribute is currently unused by the framework.
// Keep it internal to avoid committing to it as part of the public API surface.
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
internal sealed class CommandAttribute : Attribute
{
}
