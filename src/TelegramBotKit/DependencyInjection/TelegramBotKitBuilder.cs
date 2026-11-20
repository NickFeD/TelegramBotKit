using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TelegramBotKit.Dispatching;
using TelegramBotKit.Messaging;
using TelegramBotKit.Middleware;

namespace TelegramBotKit.DependencyInjection;

/// <summary>
/// Configures update routes, middleware and messaging before the dispatcher is resolved.
/// </summary>
public sealed class TelegramBotKitBuilder
{
    private readonly List<Func<IServiceProvider, IUpdateMiddleware>> _middlewareFactories = new();
    internal UpdateHandlerRegistry Registry { get; } = new();

    internal IReadOnlyList<Func<IServiceProvider, IUpdateMiddleware>> MiddlewareFactories => _middlewareFactories;
    internal TelegramBotKitBuilder(IServiceCollection services) => Services = services;

    /// <summary>
    /// Gets the service collection used to configure the bot.
    /// </summary>
    public IServiceCollection Services { get; }


    /// <summary>
    /// Adds global middleware resolved from the current update scope, outermost first.
    /// </summary>
    /// <typeparam name="TMiddleware">The middleware type resolved from the current update scope.</typeparam>
    /// <returns>The same builder for further configuration.</returns>
    public TelegramBotKitBuilder UseMiddleware<TMiddleware>()
        where TMiddleware : class, IUpdateMiddleware
    {
        Registry.EnsureMutable();
        Services.TryAddScoped<TMiddleware>();
        _middlewareFactories.Add(sp => sp.GetRequiredService<TMiddleware>());
        return this;
    }

    /// <summary>
    /// Adds global middleware whose continuation controls downstream processing.
    /// </summary>
    /// <param name="middleware">The middleware delegate, invoked in registration order, outermost first.</param>
    /// <returns>The same builder for further configuration.</returns>
    public TelegramBotKitBuilder UseMiddleware(Func<BotContext, BotContextDelegate, Task> middleware)
    {
        if (middleware is null) throw new ArgumentNullException(nameof(middleware));

        Registry.EnsureMutable();
        _middlewareFactories.Add(_ => new InlineUpdateMiddleware(middleware));
        return this;
    }

    /// <summary>
    /// Adds global middleware that returns a <see cref="ValueTask"/> and can short-circuit processing.
    /// </summary>
    /// <param name="middleware">The middleware delegate, invoked in registration order, outermost first.</param>
    /// <returns>The same builder for further configuration.</returns>
    public TelegramBotKitBuilder UseMiddleware(Func<BotContext, BotContextDelegate, ValueTask> middleware)
    {
        if (middleware is null) throw new ArgumentNullException(nameof(middleware));

        Registry.EnsureMutable();
        _middlewareFactories.Add(_ => new InlineUpdateMiddlewareValueTask(middleware));
        return this;
    }
    /// <summary>
    /// Replaces the message sender with the in-memory queued sender and configures its delivery limits.
    /// </summary>
    /// <param name="configure">An optional delegate that configures queue capacity, throttling and retries.</param>
    /// <returns>The same builder for further configuration.</returns>
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
    /// <param name="route">The descriptor identifying the Telegram update type and payload extractor.</param>
    /// <typeparam name="TPayload">The payload type extracted by the route.</typeparam>
    /// <returns>The builder for the selected route.</returns>
    public UpdateRouteBuilder<TPayload> Route<TPayload>(UpdateRoute<TPayload> route)
        where TPayload : class
    {
        ArgumentNullException.ThrowIfNull(route);
        return new(Services, Registry.GetOrAdd(route));
    }
}
