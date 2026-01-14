namespace TelegramBotKit.Commands;

/// <summary>
/// Метаданные для текстовой команды (точное совпадение по тексту сообщения).
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class TextCommandAttribute : Attribute
{
    public TextCommandAttribute(params string[] triggers)
        : this(ignoreCase: true, triggers)
    {
    }

    public TextCommandAttribute(bool ignoreCase, params string[] triggers)
    {
        if (triggers is null || triggers.Length == 0)
            throw new ArgumentException("At least one trigger is required.", nameof(triggers));

        Triggers = triggers;
        IgnoreCase = ignoreCase;
    }

    public IReadOnlyList<string> Triggers { get; }
    public bool IgnoreCase { get; }
}
