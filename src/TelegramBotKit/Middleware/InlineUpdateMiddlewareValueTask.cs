namespace TelegramBotKit.Middleware;

/// <summary>
/// Wraps an inline middleware delegate into <see cref="IUpdateMiddleware"/>.
/// Uses <see cref="ValueTask"/> internally to avoid allocating a new <see cref="Task"/>
/// when the middleware completes synchronously.
/// </summary>
internal sealed class InlineUpdateMiddlewareValueTask : IUpdateMiddleware
{
    private readonly Func<BotContext, BotContextDelegate, ValueTask> _middleware;

    public InlineUpdateMiddlewareValueTask(Func<BotContext, BotContextDelegate, ValueTask> middleware)
        => _middleware = middleware ?? throw new ArgumentNullException(nameof(middleware));

    public Task InvokeAsync(BotContext ctx, BotContextDelegate next)
    {
        var vt = _middleware(ctx, next);
        return vt.IsCompletedSuccessfully ? Task.CompletedTask : vt.AsTask();
    }
}
