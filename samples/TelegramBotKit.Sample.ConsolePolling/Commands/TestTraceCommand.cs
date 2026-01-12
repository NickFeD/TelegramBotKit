using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramBotKit;
using TelegramBotKit.Commands;

[Command]
public sealed class TestTraceCommand : ICallbackCommand
{
    public string Key => "test_trace";

    public async Task HandleAsync(CallbackQuery query, string[] args, BotContext ctx, CancellationToken ct)
    {
        await ctx.BotClient.AnswerCallbackQuery(query.Id, cancellationToken: ct);

        var chatId = query.Message?.Chat.Id;
        if (chatId is null)
            return;

        var traceId = ctx.Items.TryGetValue("traceId", out var v) ? v?.ToString() : "no-trace";

        await ctx.BotClient.SendMessage(
            chatId: chatId.Value,
            text: $"Middleware Items test: traceId={traceId}",
            cancellationToken: ct);
    }
}
