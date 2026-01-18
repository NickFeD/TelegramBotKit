using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
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
    private readonly List<Action<UpdateHandlerRegistry>> _registryActions = new();

    internal IReadOnlyList<Func<IServiceProvider, IUpdateMiddleware>> MiddlewareFactories => _middlewareFactories;
    internal IReadOnlyList<Action<UpdateHandlerRegistry>> RegistryActions => _registryActions;
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
        // Prefer DI resolution if the middleware is registered (e.g. as a singleton),
        // otherwise fall back to ActivatorUtilities for convenience.
        _middlewareFactories.Add(sp =>
            sp.GetService<TMiddleware>() ?? ActivatorUtilities.CreateInstance<TMiddleware>(sp));
        return this;
    }

    /// <summary>
    /// Adds an inline middleware (ASP.NET-style).
    /// </summary>
    public TelegramBotKitBuilder UseMiddleware(Func<BotContext, BotContextDelegate, Task> middleware)
    {
        if (middleware is null) throw new ArgumentNullException(nameof(middleware));

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

        _middlewareFactories.Add(_ => new InlineUpdateMiddlewareValueTask(middleware));
        return this;
    }
    /// <summary>
    /// Adds the update handler.
    /// </summary>
    public TelegramBotKitBuilder AddUpdateHandler<TPayload, THandler>(ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TPayload : class
        where THandler : class, IUpdatePayloadHandler<TPayload>
    {
        Services.AddUpdateHandler<TPayload, THandler>(lifetime);
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

    /// <summary>
    /// Maps an update to a payload.
    /// </summary>
    public TelegramBotKitBuilder Map<TPayload>(UpdateType type, Func<Update, TPayload?> extractor)
        where TPayload : class
    {
        if (extractor is null) throw new ArgumentNullException(nameof(extractor));

        _registryActions.Add(reg => reg.Map(type, extractor));
        return this;
    }
}
