namespace TelegramBotKit.Middleware;

/// <summary>
/// Provides a bot context delegate.
/// </summary>
public delegate Task BotContextDelegate(BotContext ctx);

internal sealed class MiddlewarePipeline
{
    private readonly Func<IServiceProvider, IUpdateMiddleware>[] _middlewareFactories;

    public MiddlewarePipeline(
        IEnumerable<Func<IServiceProvider, IUpdateMiddleware>> middlewareFactories)
    {
        _middlewareFactories = (middlewareFactories ?? Array.Empty<Func<IServiceProvider, IUpdateMiddleware>>()).ToArray();
    }

    public BotContextDelegate Build(BotContextDelegate terminal)
    {
        if (terminal is null) throw new ArgumentNullException(nameof(terminal));

        // Fast path: no middlewares registered.
        if (_middlewareFactories.Length == 0)
            return terminal;

        BotContextDelegate app = terminal;
        for (int i = _middlewareFactories.Length - 1; i >= 0; i--)
        {
            var factory = _middlewareFactories[i];
            var next = app;
            app = ctx => factory(ctx.Services).InvokeAsync(ctx, next);
        }

        return app;
    }
}
