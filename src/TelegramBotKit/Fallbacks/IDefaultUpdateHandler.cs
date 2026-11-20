namespace TelegramBotKit.Fallbacks;

/// <summary>
/// Provides fallback processing when routing cannot select a payload or terminal handler.
/// </summary>
public interface IDefaultUpdateHandler
{
    /// <summary>
    /// Processes an update for which routing found no applicable terminal handler.
    /// </summary>
    /// <param name="ctx">The current update context, including scoped services and cancellation.</param>
    /// <returns>The asynchronous operation, which completes after processing finishes.</returns>
    Task HandleAsync(BotContext ctx);
}
