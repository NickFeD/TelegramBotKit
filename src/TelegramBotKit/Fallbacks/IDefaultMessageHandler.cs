using Telegram.Bot.Types;

namespace TelegramBotKit.Fallbacks;

/// <summary>
/// Вызывается, когда Message дошёл до MessageUpdateHandler, но ни одна команда/триггер не подошла.
/// Сюда попадут и "unknown /command", и обычный текст, и не-текстовые сообщения.
/// </summary>
public interface IDefaultMessageHandler
{
    Task HandleAsync(Message message, BotContext ctx, CancellationToken ct);
}
