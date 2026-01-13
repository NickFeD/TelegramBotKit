namespace TelegramBotKit;

public abstract class TelegramBotKitException : Exception
{
    protected TelegramBotKitException(string message) : base(message) { }
    protected TelegramBotKitException(string message, Exception inner) : base(message, inner) { }
}
