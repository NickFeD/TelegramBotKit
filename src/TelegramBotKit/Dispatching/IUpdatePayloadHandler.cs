using Telegram.Bot.Types;

namespace TelegramBotKit.Dispatching;

/// <summary>
/// Обработчик конкретного payload из Update (Message, CallbackQuery, InlineQuery, ...).
/// </summary>
public interface IUpdatePayloadHandler<TPayload> where TPayload : class
{
    Task HandleAsync(TPayload payload, BotContext ctx);
}

// NOTE:
// Раньше здесь были «маркерные» интерфейсы вроде IInlineQueryHandler и т.п.
// От них отказались намеренно:
// 1) они усложняют API;
// 2) в стандартном DI .NET регистрация как IInlineQueryHandler НЕ делает сервис
//    доступным как IUpdatePayloadHandler<InlineQuery>.
// Вместо этого используется только generic-обработчик + маппинг UpdateType -> payload.
