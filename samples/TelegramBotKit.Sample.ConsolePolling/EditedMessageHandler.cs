using Microsoft.Extensions.Logging;
using Telegram.Bot.Types;
using TelegramBotKit.Dispatching;

namespace TelegramBotKit.Sample.ConsolePolling;

public sealed class EditedMessageHandler(ILogger<EditedMessageHandler> log) : IUpdatePayloadHandler<Message>
{
    public Task HandleAsync(Message payload, BotContext ctx)
    {
        log.LogInformation("Message {MessageId} was edited in chat {ChatId}", payload.Id, payload.Chat.Id);
        return Task.CompletedTask;
    }
}
