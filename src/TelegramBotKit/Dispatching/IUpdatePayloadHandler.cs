namespace TelegramBotKit.Dispatching;

/// <summary>
/// Обработчик конкретного payload из Update (Message, CallbackQuery, InlineQuery, ...).
/// </summary>
public interface IUpdatePayloadHandler<TPayload> where TPayload : class
{
    Task HandleAsync(TPayload payload, BotContext ctx, CancellationToken ct);
}