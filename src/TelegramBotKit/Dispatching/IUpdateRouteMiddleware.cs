namespace TelegramBotKit.Dispatching;

/// <summary>
/// Defines route-local middleware that can inspect the payload selected for one typed update route.
/// </summary>
/// <typeparam name="TPayload">The payload selected by the update route.</typeparam>
public interface IUpdateRouteMiddleware<TPayload> where TPayload : class
{
    /// <summary>
    /// Executes route-local behavior and optionally invokes the downstream route continuation.
    /// Omitting <paramref name="next"/> short-circuits the route before its terminal handler.
    /// </summary>
    /// <param name="context">The route context containing the selected payload.</param>
    /// <param name="next">The continuation for the remaining route middleware and terminal.</param>
    /// <returns>The asynchronous operation, which completes after processing finishes.</returns>
    Task InvokeAsync(UpdateRouteContext<TPayload> context, UpdateRouteDelegate<TPayload> next);
}
