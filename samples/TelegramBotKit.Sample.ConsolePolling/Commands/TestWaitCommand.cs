using Telegram.Bot.Types;
using TelegramBotKit.Commands;
using TelegramBotKit.Conversations;
using TelegramBotKit.Messaging;

namespace TelegramBotKit.Sample.ConsolePolling.Commands;

[CallbackCommand("test_wait")]
public sealed class TestWaitCommand : ICallbackCommand
{
    public async Task HandleAsync(CallbackQuery query, string[] args, BotContext ctx)
    {
        if (query.Message is null)
        {
            await ctx.Sender.AnswerCallback(query.Id, new AnswerCallback { Text = "Нет message в callback" }, ctx.CancellationToken);
            return;
        }

        await ctx.Sender.AnswerCallback(query.Id, new AnswerCallback { Text = "Ок, жду сообщение…" }, ctx.CancellationToken);

        await ctx.Sender.SendText(
            chatId: query.Message.Chat.Id,
            msg: new SendText { Text = "Напиши любое сообщение в течение 30 секунд." },
            ct: ctx.CancellationToken);

        var wait = ctx.GetRequiredService<WaitForUserResponse>();

        var msg = await wait.WaitAsync(
            chatId: query.Message.Chat.Id,
            userId: query.From.Id,
            timeout: TimeSpan.FromSeconds(30),
            ct: ctx.CancellationToken);

        if (msg is null)
        {
            await ctx.Sender.SendText(
                chatId: query.Message.Chat.Id,
                msg: new SendText { Text = "Таймаут ⏰" },
                ct: ctx.CancellationToken);
            return;
        }

        await ctx.Sender.SendText(
            chatId: query.Message.Chat.Id,
            msg: new SendText { Text = $"Получил: {msg.Text ?? "<non-text>"}" },
            ct: ctx.CancellationToken);
    }
}
