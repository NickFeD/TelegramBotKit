namespace TelegramBotKit.Commands;

/// <summary>
/// Метаданные для callback-команды. Key — первый токен в callback_data.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class CallbackCommandAttribute : Attribute
{
    public CallbackCommandAttribute(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key must not be empty.", nameof(key));

        Key = key;
    }

    public string Key { get; }
}
