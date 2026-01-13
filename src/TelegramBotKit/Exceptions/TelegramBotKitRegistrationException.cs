namespace TelegramBotKit;

public sealed class TelegramBotKitRegistrationException : TelegramBotKitException
{
    public TelegramBotKitRegistrationException(string message) : base(message) { }
    public TelegramBotKitRegistrationException(string message, Exception inner) : base(message, inner) { }
}
