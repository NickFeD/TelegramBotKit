namespace TelegramBotKit.Commands;

/// <summary>
/// Declares exact text triggers used by the bundled generator to register an <see cref="ITextCommand"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class TextCommandAttribute : Attribute
{
    /// <summary>
    /// Declares text triggers matched with ordinal, case-insensitive comparison.
    /// </summary>
    /// <param name="triggers">The text triggers; registration trims values and removes empty or duplicate entries.</param>
    public TextCommandAttribute(params string[] triggers)
        : this(ignoreCase: true, triggers)
    {
    }

    /// <summary>
    /// Declares text triggers with an explicit ordinal case-comparison mode.
    /// </summary>
    /// <param name="ignoreCase">Whether text matching ignores case using ordinal comparison.</param>
    /// <param name="triggers">The text triggers; registration trims values and removes empty or duplicate entries.</param>
    public TextCommandAttribute(bool ignoreCase, params string[] triggers)
    {
        if (triggers is null || triggers.Length == 0)
            throw new ArgumentException("At least one trigger is required.", nameof(triggers));

        Triggers = triggers;
        IgnoreCase = ignoreCase;
    }

    /// <summary>
    /// Gets the declared triggers, which are trimmed and deduplicated during registration.
    /// </summary>
    public IReadOnlyList<string> Triggers { get; }
    /// <summary>
    /// Gets a value indicating whether triggers are case-insensitive.
    /// </summary>
    public bool IgnoreCase { get; }
}
