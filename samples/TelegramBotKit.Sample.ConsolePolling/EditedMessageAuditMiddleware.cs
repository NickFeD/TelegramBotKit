using Microsoft.Extensions.Logging;
using Telegram.Bot.Types;
using TelegramBotKit.Dispatching;

namespace TelegramBotKit.Sample.ConsolePolling;

public sealed class EditedMessageAuditMiddleware(ILogger<EditedMessageAuditMiddleware> log)
    : IUpdateRouteMiddleware<Message>
{
    public async Task InvokeAsync(
        UpdateRouteContext<Message> context,
        UpdateRouteDelegate<Message> next)
    {
        log.LogInformation(
            "Routing edited message {MessageId} from chat {ChatId}",
            context.Payload.Id,
            context.Payload.Chat.Id);

        await next(context);
    }
}
