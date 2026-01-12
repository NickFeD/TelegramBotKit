using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramBotKit;
using TelegramBotKit.Commands;

[Command]
public sealed class TestCallbackCommand : ICallbackCommand
{
    public string Key => "test_callback";

    public async Task HandleAsync(CallbackQuery query, string[] args, BotContext ctx, CancellationToken ct)
    {
        // Быстрый ACK, чтобы Telegram не крутил "loading"
        await ctx.BotClient.AnswerCallbackQuery(
            callbackQueryId: query.Id,
            text: "CallbackQuery получен ✅",
            cancellationToken: ct);

        var chatId = query.Message?.Chat.Id;
        if (chatId is null)
            return;

        await ctx.BotClient.SendMessage(
            chatId: chatId.Value,
            text: $"Callback test OK. Data = \"{query.Data}\"",
            cancellationToken: ct);
    }
}