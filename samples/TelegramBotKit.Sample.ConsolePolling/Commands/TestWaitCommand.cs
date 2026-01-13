using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramBotKit.Commands;
using TelegramBotKit.Conversations;

namespace TelegramBotKit.Sample.ConsolePolling.Commands;

[Command]
public sealed class TestWaitCommand : ICallbackCommand
{
    public string Key => "test_wait";

    public async Task HandleAsync(CallbackQuery query, string[] args, BotContext ctx, CancellationToken ct)
    {
        if (query.Message is null)
        {
            await ctx.BotClient.AnswerCallbackQuery(query.Id, "Нет message в callback", cancellationToken: ct);
            return;
        }

        await ctx.BotClient.AnswerCallbackQuery(query.Id, "Ок, жду сообщение…", cancellationToken: ct);

        await ctx.BotClient.SendMessage(
            chatId: query.Message.Chat.Id,
            text: "Напиши любое сообщение в течение 30 секунд.",
            cancellationToken: ct);

        var wait = ctx.GetRequiredService<WaitForUserResponse>();

        var msg = await wait.WaitAsync(
            chatId: query.Message.Chat.Id,
            userId: query.From.Id,
            timeout: TimeSpan.FromSeconds(30),
            ct: ct);

        if (msg is null)
        {
            await ctx.BotClient.SendMessage(
                chatId: query.Message.Chat.Id,
                text: "Таймаут ⏰",
                cancellationToken: ct);
            return;
        }

        await ctx.BotClient.SendMessage(
            chatId: query.Message.Chat.Id,
            text: $"Получил: {msg.Text ?? "<non-text>"}",
            cancellationToken: ct);
    }
}
