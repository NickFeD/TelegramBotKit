using Telegram.Bot.Types;
using TelegramBotKit.Commands;
using TelegramBotKit.Messaging;

namespace TelegramBotKit.Sample.MinimalPolling;

[MessageCommand("/start")]
public sealed class StartCommand : IMessageCommand
{
    public Task HandleAsync(Message message, BotContext context) =>
        context.Sender.SendText(
            message.Chat.Id,
            new SendText { Text = "Hello from TelegramBotKit." },
            context.CancellationToken);
}
