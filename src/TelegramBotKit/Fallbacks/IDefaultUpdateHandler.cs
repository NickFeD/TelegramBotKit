namespace TelegramBotKit.Fallbacks;

/// <summary>
/// Вызывается, когда апдейт не был обработан никем (нет обработчика под UpdateType).
/// </summary>
public interface IDefaultUpdateHandler
{
    Task HandleAsync(BotContext ctx);
}
