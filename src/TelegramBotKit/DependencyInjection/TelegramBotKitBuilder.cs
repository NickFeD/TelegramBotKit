using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramBotKit.Commands;
using TelegramBotKit.Dispatching;
using TelegramBotKit.Messaging;
using TelegramBotKit.Middleware;

namespace TelegramBotKit.DependencyInjection;

/// <summary>
/// Provides a telegram bot kit builder.
/// </summary>
public sealed class TelegramBotKitBuilder
{
    private readonly List<Type> _middlewareTypes = new();
    private readonly List<Action<UpdateHandlerRegistry>> _registryActions = new();

    internal IReadOnlyList<Type> MiddlewareTypes => _middlewareTypes;
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
        _middlewareTypes.Add(typeof(TMiddleware));
        return this;
    }

    /// <summary>
    /// Adds the command.
    /// </summary>
    public TelegramBotKitBuilder AddCommand<TCommand>(ServiceLifetime lifetime = ServiceLifetime.Transient)
        where TCommand : class, ICommand
    {
        Services.AddCommand(typeof(TCommand), lifetime);
        return this;
    }

    /// <summary>
    /// Adds the commands from assembly.
    /// </summary>
    public TelegramBotKitBuilder AddCommandsFromAssembly<TMarker>()
    {
        Services.AddCommandsFromAssemblies(typeof(TMarker).Assembly);
        return this;
    }

    /// <summary>
    /// Adds the commands from assemblies.
    /// </summary>
    public TelegramBotKitBuilder AddCommandsFromAssemblies(params Assembly[] assemblies)
    {
        Services.AddCommandsFromAssemblies(assemblies);
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
