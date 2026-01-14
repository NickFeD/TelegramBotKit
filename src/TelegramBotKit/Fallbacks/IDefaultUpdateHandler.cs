namespace TelegramBotKit.Fallbacks;

/// <summary>
/// Defines the contract for a default update handler.
/// </summary>
public interface IDefaultUpdateHandler
{
    /// <summary>
    /// Handles the update asynchronously.
    /// </summary>
    Task HandleAsync(BotContext ctx);
}
