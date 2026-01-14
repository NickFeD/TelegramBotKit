namespace TelegramBotKit.Commands;

/// <summary>
/// Marks a callback command.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class CallbackCommandAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of CallbackCommandAttribute.
    /// </summary>
    public CallbackCommandAttribute(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key must not be empty.", nameof(key));

        Key = key;
    }

    /// <summary>
    /// Gets the key.
    /// </summary>
    public string Key { get; }
}
