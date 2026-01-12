namespace TelegramBotKit.Commands;

/// <summary>
/// Маячок: если класс помечен этим атрибутом — он считается командой при авто-регистрации.
/// Если атрибута нет — класс игнорируется.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class CommandAttribute : Attribute
{
}
