namespace TelegramBotKit.Commands;

/// <summary>
/// Declares the callback-data key used by the bundled generator to register an <see cref="ICallbackCommand"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class CallbackCommandAttribute : Attribute
{
    /// <summary>
    /// Declares the callback routing key and generated mapping for the attributed handler.
    /// </summary>
    /// <param name="key">The callback routing key. It must be nonempty and contain no spaces.</param>
    public CallbackCommandAttribute(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key must not be empty.", nameof(key));

        Key = key;
    }

    /// <summary>
    /// Gets the declared callback key; routing matches it against the first callback-data token.
    /// </summary>
    public string Key { get; }
}
