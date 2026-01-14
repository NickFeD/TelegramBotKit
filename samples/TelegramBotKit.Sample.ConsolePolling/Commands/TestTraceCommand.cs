using Telegram.Bot.Types;
using TelegramBotKit.Commands;
using TelegramBotKit.Messaging;

namespace TelegramBotKit.Sample.ConsolePolling.Commands;

[CallbackCommand("test_trace")]
public sealed class TestTraceCommand : ICallbackCommand
{
    public async Task HandleAsync(CallbackQuery query, string[] args, BotContext ctx)
    {
        var trace = ctx.Items.TryGetValue("traceId", out var v) ? v?.ToString() : "no-trace";

        await ctx.Sender.AnswerCallback(
            callbackQueryId: query.Id,
            answer: new AnswerCallback { Text = "trace отправлю в чат" },
            ct: ctx.CancellationToken);

        if (query.Message is not null)
        {
            await ctx.Sender.SendText(
                chatId: query.Message.Chat.Id,
                msg: new SendText { Text = $"traceId из middleware: {trace}" },
                ct: ctx.CancellationToken);
        }
    }
}
