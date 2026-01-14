using Telegram.Bot.Types;
using TelegramBotKit.Fallbacks;
using TelegramBotKit.Messaging;

namespace TelegramBotKit.Sample.ConsolePolling;

/// <summary>
/// Одна реализация сразу для всех default-интерфейсов (удобно для sample).
/// </summary>
public sealed class SampleDefaultHandlers :
    IDefaultMessageHandler,
    IDefaultCallbackHandler,
    IDefaultUpdateHandler
{
    public Task HandleAsync(BotContext ctx)
        => Task.CompletedTask;

    public Task HandleAsync(Message message, BotContext ctx)
    {
        // non-text / или "ничего не сматчилось" (если сюда попали)
        return ctx.Sender.SendText(
            chatId: message.Chat.Id,
            msg: new SendText { Text = "Я такое не обрабатываю. Попробуй /start" },
            ct: ctx.CancellationToken);
    }

    public Task HandleAsync(CallbackQuery query, BotContext ctx)
    {
        return ctx.Sender.AnswerCallback(
            callbackQueryId: query.Id,
            answer: new AnswerCallback { Text = "Неизвестная кнопка (fallback)" },
            ct: ctx.CancellationToken);
    }
}
