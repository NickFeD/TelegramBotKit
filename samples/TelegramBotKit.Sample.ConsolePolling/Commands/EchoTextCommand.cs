using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramBotKit;
using TelegramBotKit.Commands;

[Command]
public sealed class EchoTextCommand : ITextCommand
{
    public IReadOnlyCollection<string> Triggers => new[] { "эхо", "echo" };
    public bool IgnoreCase => true;

    public async Task HandleAsync(Message message, BotContext ctx, CancellationToken ct)
    {
        await ctx.BotClient.SendMessage(
            chatId: message.Chat.Id,
            text: $"Ты написал: {message.Text}",
            cancellationToken: ct);
    }
}
