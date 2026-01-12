using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramBotKit;
using TelegramBotKit.Commands;

[Command]
public sealed class LikeCallbackCommand : ICallbackCommand
{
    public string Key => "like";

    public async Task HandleAsync(CallbackQuery query, string[] args, BotContext ctx, CancellationToken ct)
    {
        // Быстрый ответ на callback
        await ctx.BotClient.AnswerCallbackQuery(
            callbackQueryId: query.Id,
            text: "❤️",
            cancellationToken: ct);
    }
}
