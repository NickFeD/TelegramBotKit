using Microsoft.Extensions.DependencyInjection;

namespace TelegramBotKit.Middleware;

/// <summary>
/// Provides a bot context delegate.
/// </summary>
public delegate Task BotContextDelegate(BotContext ctx);

internal sealed class MiddlewarePipeline
{
    private readonly IServiceProvider _rootServices;
    private readonly Type[] _middlewareTypes;

    public MiddlewarePipeline(IServiceProvider rootServices, IEnumerable<Type> middlewareTypes)
    {
        _rootServices = rootServices ?? throw new ArgumentNullException(nameof(rootServices));
        _middlewareTypes = (middlewareTypes ?? Array.Empty<Type>()).ToArray();

        for (int i = 0; i < _middlewareTypes.Length; i++)
        {
            var t = _middlewareTypes[i];
            if (!typeof(IUpdateMiddleware).IsAssignableFrom(t))
                throw new ArgumentException($"Middleware type must implement IUpdateMiddleware: {t.FullName}");
        }
    }

    public BotContextDelegate Build(BotContextDelegate terminal)
    {
        if (terminal is null) throw new ArgumentNullException(nameof(terminal));

        var middlewares = new IUpdateMiddleware[_middlewareTypes.Length];
        for (int i = 0; i < _middlewareTypes.Length; i++)
        {
            middlewares[i] = (IUpdateMiddleware)ActivatorUtilities.CreateInstance(_rootServices, _middlewareTypes[i]);
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
