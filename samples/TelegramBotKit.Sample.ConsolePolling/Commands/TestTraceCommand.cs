using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramBotKit.Commands;

namespace TelegramBotKit.Sample.ConsolePolling.Commands;

[Command]
public sealed class TestTraceCommand : ICallbackCommand
{
    public string Key => "test_trace";

    public async Task HandleAsync(CallbackQuery query, string[] args, BotContext ctx, CancellationToken ct)
    {
        var trace = ctx.Items.TryGetValue("traceId", out var v) ? v?.ToString() : "no-trace";

        await ctx.BotClient.AnswerCallbackQuery(
            callbackQueryId: query.Id,
            text: "trace отправлю в чат",
            cancellationToken: ct);

        if (query.Message is not null)
        {
            await ctx.BotClient.SendMessage(
                chatId: query.Message.Chat.Id,
                text: $"traceId из middleware: {trace}",
                cancellationToken: ct);
        }
    }
}
