namespace TelegramBotKit;

public sealed class TelegramBotKitDispatchException : TelegramBotKitException
{
    public TelegramBotKitDispatchException(string message) : base(message) { }
    public TelegramBotKitDispatchException(string message, Exception inner) : base(message, inner) { }
}
