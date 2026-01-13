namespace TelegramBotKit;

public sealed class TelegramBotKitCallbackDataException : TelegramBotKitException
{
    public TelegramBotKitCallbackDataException(string message) : base(message) { }
    public TelegramBotKitCallbackDataException(string message, Exception inner) : base(message, inner) { }
}
