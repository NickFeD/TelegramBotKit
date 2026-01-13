namespace TelegramBotKit;

public sealed class TelegramBotKitConfigurationException : TelegramBotKitException
{
    public TelegramBotKitConfigurationException(string message) : base(message) { }
    public TelegramBotKitConfigurationException(string message, Exception inner) : base(message, inner) { }
}
