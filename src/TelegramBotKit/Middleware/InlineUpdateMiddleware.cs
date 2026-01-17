namespace TelegramBotKit.Middleware;

/// <summary>
/// Wraps an inline middleware delegate into <see cref="IUpdateMiddleware"/>.
/// </summary>
internal sealed class InlineUpdateMiddleware : IUpdateMiddleware
{
    private readonly Func<BotContext, BotContextDelegate, Task> _middleware;

    public InlineUpdateMiddleware(Func<BotContext, BotContextDelegate, Task> middleware)
    {
        _middleware = middleware ?? throw new ArgumentNullException(nameof(middleware));
    }

    public Task InvokeAsync(BotContext ctx, BotContextDelegate next)
        => _middleware(ctx, next);
}
