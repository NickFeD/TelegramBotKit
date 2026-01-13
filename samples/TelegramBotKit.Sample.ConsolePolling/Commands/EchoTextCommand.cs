using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramBotKit.Commands;

namespace TelegramBotKit.Sample.ConsolePolling.Commands;

[Command]
public sealed class EchoTextCommand : ITextCommand
{
    public IReadOnlyCollection<string> Triggers => new[] { "echo", "эхо" };
    public bool IgnoreCase => true;

    public async Task HandleAsync(Message message, BotContext ctx, CancellationToken ct)
    {
        await ctx.BotClient.SendMessage(
            chatId: message.Chat.Id,
            text: $"echo: {message.Text}",
            cancellationToken: ct);
    }
}
