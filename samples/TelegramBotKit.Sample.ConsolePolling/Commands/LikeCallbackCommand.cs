using Telegram.Bot.Types;
using TelegramBotKit.Commands;
using TelegramBotKit.Messaging;

namespace TelegramBotKit.Sample.ConsolePolling.Commands;

[CallbackCommand("like")]
public sealed class LikeCallbackCommand : ICallbackCommand
{
    public Task HandleAsync(CallbackQuery query, string[] args, BotContext ctx)
    {
        var id = args.Length > 0 ? args[0] : "?";

        return ctx.Sender.AnswerCallback(
            callbackQueryId: query.Id,
            answer: new AnswerCallback { Text = $"Лайк принят ❤️ (id={id})" },
            ct: ctx.CancellationToken);
    }
}
