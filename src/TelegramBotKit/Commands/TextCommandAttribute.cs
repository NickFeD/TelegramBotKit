namespace TelegramBotKit.Commands;

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

    /// <summary>
    /// Gets the triggers.
    /// </summary>
    public IReadOnlyList<string> Triggers { get; }
    /// <summary>
    /// Gets a value indicating whether triggers are case-insensitive.
    /// </summary>
    public bool IgnoreCase { get; }
}
