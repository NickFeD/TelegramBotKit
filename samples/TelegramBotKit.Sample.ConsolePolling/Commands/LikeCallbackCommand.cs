using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramBotKit.Commands;

namespace TelegramBotKit.Sample.ConsolePolling.Commands;

[Command]
public sealed class LikeCallbackCommand : ICallbackCommand
{
    public string Key => "like";

    public async Task HandleAsync(CallbackQuery query, string[] args, BotContext ctx, CancellationToken ct)
    {
        var id = args.Length > 0 ? args[0] : "?";

        await ctx.BotClient.AnswerCallbackQuery(
            callbackQueryId: query.Id,
            text: $"Лайк принят ❤️ (id={id})",
            cancellationToken: ct);
    }
}
