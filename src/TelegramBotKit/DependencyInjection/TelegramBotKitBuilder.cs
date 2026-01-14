using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramBotKit.Commands;
using TelegramBotKit.Dispatching;
using TelegramBotKit.Messaging;
using TelegramBotKit.Middleware;

namespace TelegramBotKit.DependencyInjection;

public sealed class TelegramBotKitBuilder
{
    private readonly List<Type> _middlewareTypes = new();
    private readonly List<Action<UpdateHandlerRegistry>> _registryActions = new();

    internal IReadOnlyList<Type> MiddlewareTypes => _middlewareTypes;
    internal IReadOnlyList<Action<UpdateHandlerRegistry>> RegistryActions => _registryActions;
    internal TelegramBotKitBuilder(IServiceCollection services) => Services = services;

    public IServiceCollection Services { get; }


    /// <summary>
    /// Добавить middleware-класс в пайплайн.
    /// </summary>
    public TelegramBotKitBuilder UseMiddleware<TMiddleware>()
        where TMiddleware : class, IUpdateMiddleware
    {
        _middlewareTypes.Add(typeof(TMiddleware));
        return this;
    }

    public TelegramBotKitBuilder AddCommand<TCommand>(ServiceLifetime lifetime = ServiceLifetime.Transient)
        where TCommand : class, ICommand
    {
        Services.AddCommand(typeof(TCommand), lifetime);
        return this;
    }

    public TelegramBotKitBuilder AddCommandsFromAssembly<TMarker>()
    {
        Services.AddCommandsFromAssemblies(typeof(TMarker).Assembly);
        return this;
    }

    public TelegramBotKitBuilder AddCommandsFromAssemblies(params Assembly[] assemblies)
    {
        Services.AddCommandsFromAssemblies(assemblies);
        return this;
    }

    /// <summary>
    /// Удобная регистрация обработчика payload-типа.
    /// Эквивалент Services.AddUpdateHandler&lt;TPayload, THandler&gt;(...).
    /// </summary>
    public TelegramBotKitBuilder AddUpdateHandler<TPayload, THandler>(ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TPayload : class
        where THandler : class, IUpdatePayloadHandler<TPayload>
    {
        Services.AddUpdateHandler<TPayload, THandler>(lifetime);
        return this;
    }

    /// <summary>
    /// Дополнительно замаппить UpdateType на payload.
    /// Дополнительно замаппить UpdateType на payload (редко нужно, т.к. большинство типов маппятся по соглашению).
    /// </summary>

    /// <summary>
    /// Включить очередной IMessageSender (Channel + retry-after + throttling).
    /// По умолчанию sender без очереди.
    /// </summary>
    public TelegramBotKitBuilder UseQueuedMessageSender(Action<QueuedMessageSenderOptions>? configure = null)
    {
        Services.AddTelegramBotKitQueuedMessageSender(configure);
        return this;
    }

    public TelegramBotKitBuilder Map<TPayload>(UpdateType type, Func<Update, TPayload?> extractor)
        where TPayload : class
    {
        if (extractor is null) throw new ArgumentNullException(nameof(extractor));

        _registryActions.Add(reg => reg.Map(type, extractor));
        return this;
    }
}
