using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramBotKit.Commands;

namespace TelegramBotKit.Sample.ConsolePolling.Commands;

[Command]
public sealed class TestCallbackCommand : ICallbackCommand
{
    public string Key => "test_callback";

    public async Task HandleAsync(CallbackQuery query, string[] args, BotContext ctx, CancellationToken ct)
    {
        await ctx.BotClient.AnswerCallbackQuery(
            callbackQueryId: query.Id,
            text: "Callback работает ✅",
            cancellationToken: ct);

        if (query.Message is not null)
        {
            await ctx.BotClient.SendMessage(
                chatId: query.Message.Chat.Id,
                text: "Я получил CallbackQuery и ответил на него.",
                cancellationToken: ct);
        }
    }
}
