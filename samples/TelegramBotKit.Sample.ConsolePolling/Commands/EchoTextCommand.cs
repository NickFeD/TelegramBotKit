using Telegram.Bot.Types;
using TelegramBotKit.Commands;
using TelegramBotKit.Messaging;

namespace TelegramBotKit.Sample.ConsolePolling.Commands;

[TextCommand("echo", "эхо")]
public sealed class EchoTextCommand : ITextCommand
{
    public Task HandleAsync(Message message, BotContext ctx)
        => ctx.Sender.SendText(
            chatId: message.Chat.Id,
            msg: new SendText { Text = $"echo: {message.Text}" },
            ct: ctx.CancellationToken);
}
