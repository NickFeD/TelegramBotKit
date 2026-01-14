namespace TelegramBotKit.Middleware;

/// <summary>
/// Defines the contract for an update middleware.
/// </summary>
public interface IUpdateMiddleware
{
    /// <summary>
    /// Invokes the next middleware asynchronously.
    /// </summary>
    Task InvokeAsync(BotContext ctx, BotContextDelegate next);
}
