using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TelegramBotKit.Dispatching;
using TelegramBotKit.Messaging;
using TelegramBotKit.Middleware;

namespace TelegramBotKit.DependencyInjection;

/// <summary>
/// Provides a telegram bot kit builder.
/// </summary>
public sealed class TelegramBotKitBuilder
{
    private readonly List<Func<IServiceProvider, IUpdateMiddleware>> _middlewareFactories = new();
    internal UpdateHandlerRegistry Registry { get; } = new();

    internal IReadOnlyList<Func<IServiceProvider, IUpdateMiddleware>> MiddlewareFactories => _middlewareFactories;
    internal TelegramBotKitBuilder(IServiceCollection services) => Services = services;

    /// <summary>
    /// Gets the service provider.
    /// </summary>
    public IServiceCollection Services { get; }


    /// <summary>
    /// Adds the middleware.
    /// </summary>
    public TelegramBotKitBuilder UseMiddleware<TMiddleware>()
        where TMiddleware : class, IUpdateMiddleware
    {
        Registry.EnsureMutable();
        Services.TryAddScoped<TMiddleware>();
        _middlewareFactories.Add(sp => sp.GetRequiredService<TMiddleware>());
        return this;
    }

    /// <summary>
    /// Adds an inline middleware (ASP.NET-style).
    /// </summary>
    public TelegramBotKitBuilder UseMiddleware(Func<BotContext, BotContextDelegate, Task> middleware)
    {
        if (middleware is null) throw new ArgumentNullException(nameof(middleware));

        Registry.EnsureMutable();
        _middlewareFactories.Add(_ => new InlineUpdateMiddleware(middleware));
        return this;
    }

    /// <summary>
    /// Adds an inline middleware using <see cref="ValueTask"/> for reduced allocations when the middleware
    /// (and/or the next delegate) often completes synchronously.
    /// </summary>
    public TelegramBotKitBuilder UseMiddleware(Func<BotContext, BotContextDelegate, ValueTask> middleware)
    {
        if (middleware is null) throw new ArgumentNullException(nameof(middleware));

        Registry.EnsureMutable();
        _middlewareFactories.Add(_ => new InlineUpdateMiddlewareValueTask(middleware));
        return this;
    }
    /// <summary>
    /// Adds the queued message sender.
    /// </summary>
    public TelegramBotKitBuilder UseQueuedMessageSender(Action<QueuedMessageSenderOptions>? configure = null)
    {
        Services.AddTelegramBotKitQueuedMessageSender(configure);
        return this;
    }

    /// <summary>Configures a route identified by its Telegram update type.</summary>
    /// <remarks>
    /// Explicit configuration owns the entire route, opting out of any built-in terminal.
    /// Reuse the same descriptor to add middleware; only one terminal may be registered.
    /// </remarks>
    public UpdateRouteBuilder<TPayload> Route<TPayload>(UpdateRoute<TPayload> route)
        where TPayload : class
    {
        ArgumentNullException.ThrowIfNull(route);
        return new(Services, Registry.GetOrAdd(route));
    }
}
