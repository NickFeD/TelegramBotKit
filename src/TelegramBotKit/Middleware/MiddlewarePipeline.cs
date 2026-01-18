namespace TelegramBotKit.Middleware;

/// <summary>
/// Provides a bot context delegate.
/// </summary>
public delegate Task BotContextDelegate(BotContext ctx);

internal sealed class MiddlewarePipeline
{
    private readonly IServiceProvider _rootServices;
    private readonly Func<IServiceProvider, IUpdateMiddleware>[] _middlewareFactories;

    public MiddlewarePipeline(
        IServiceProvider rootServices,
        IEnumerable<Func<IServiceProvider, IUpdateMiddleware>> middlewareFactories)
    {
        _rootServices = rootServices ?? throw new ArgumentNullException(nameof(rootServices));
        _middlewareFactories = (middlewareFactories ?? Array.Empty<Func<IServiceProvider, IUpdateMiddleware>>()).ToArray();
    }

    public BotContextDelegate Build(BotContextDelegate terminal)
    {
        if (terminal is null) throw new ArgumentNullException(nameof(terminal));

        // Fast path: no middlewares registered.
        if (_middlewareFactories.Length == 0)
            return terminal;

        var middlewares = new IUpdateMiddleware[_middlewareFactories.Length];
        for (int i = 0; i < _middlewareFactories.Length; i++)
        {
            var mw = _middlewareFactories[i](_rootServices);
            middlewares[i] = mw ?? throw new InvalidOperationException("Middleware factory returned null.");
        }

        BotContextDelegate app = terminal;

        for (int i = middlewares.Length - 1; i >= 0; i--)
        {
            var mw = middlewares[i];
            var next = app;
            app = ctx => mw.InvokeAsync(ctx, next);
        }

        return app;
    }
}
