namespace TelegramBotKit.Middleware;

/// <summary>
/// Defines global middleware that wraps update dispatch and can short-circuit it.
/// </summary>
public interface IUpdateMiddleware
{
    /// <summary>
    /// Processes an update and optionally invokes the remaining pipeline through <paramref name="next"/>.
    /// </summary>
    /// <param name="ctx">The current update context, including scoped services and cancellation.</param>
    /// <param name="next">The continuation; omit the call to stop downstream processing.</param>
    /// <returns>The asynchronous operation, which completes after processing finishes.</returns>
    Task InvokeAsync(BotContext ctx, BotContextDelegate next);
}
