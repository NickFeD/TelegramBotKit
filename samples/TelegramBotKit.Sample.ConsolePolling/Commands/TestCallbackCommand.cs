using Telegram.Bot.Types;
using TelegramBotKit.Commands;
using TelegramBotKit.Messaging;

namespace TelegramBotKit.Sample.ConsolePolling.Commands;

[CallbackCommand("test_callback")]
public sealed class TestCallbackCommand : ICallbackCommand
{
    public async Task HandleAsync(CallbackQuery query, string[] args, BotContext ctx)
    {
        await ctx.Sender.AnswerCallback(
            callbackQueryId: query.Id,
            answer: new AnswerCallback { Text = "Callback работает ✅" },
            ct: ctx.CancellationToken);

        if (query.Message is not null)
        {
            await ctx.Sender.SendText(
                chatId: query.Message.Chat.Id,
                msg: new SendText { Text = "Я получил CallbackQuery и ответил на него." },
                ct: ctx.CancellationToken);
        }
    }
}
