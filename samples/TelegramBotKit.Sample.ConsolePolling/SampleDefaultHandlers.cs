using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramBotKit.Fallbacks;

namespace TelegramBotKit.Sample.ConsolePolling;

/// <summary>
/// Одна реализация сразу для всех default-интерфейсов (удобно для sample).
/// </summary>
public sealed class SampleDefaultHandlers :
    IDefaultMessageHandler,
    IDefaultCallbackHandler,
    IDefaultUpdateHandler
{
    public Task HandleAsync(BotContext ctx, CancellationToken ct)
        => Task.CompletedTask;

    public Task HandleAsync(Message message, BotContext ctx, CancellationToken ct)
    {
        // non-text / или "ничего не сматчилось" (если сюда попали)
        return ctx.BotClient.SendMessage(
            chatId: message.Chat.Id,
            text: "Я такое не обрабатываю. Попробуй /start",
            cancellationToken: ct);
    }
    public Task HandleAsync(CallbackQuery query, BotContext ctx, CancellationToken ct)
    {
        return ctx.BotClient.AnswerCallbackQuery(
            callbackQueryId: query.Id,
            text: "Неизвестная кнопка (fallback)",
            cancellationToken: ct);
    }
}
