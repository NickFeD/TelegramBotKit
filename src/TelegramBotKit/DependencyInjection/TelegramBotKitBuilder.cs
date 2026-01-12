using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramBotKit.Commands;
using TelegramBotKit.Middleware;

namespace TelegramBotKit.DependencyInjection;

public sealed class TelegramBotKitBuilder
{
    internal TelegramBotKitBuilder(IServiceCollection services) => Services = services;

    public IServiceCollection Services { get; }

    // Middleware
    public TelegramBotKitBuilder Use(UpdateMiddleware middleware)
    {
        if (middleware is null) throw new ArgumentNullException(nameof(middleware));
        Services.AddSingleton<IMiddlewareConfigurator>(new DelegateMiddlewareConfigurator(p => p.Use(middleware)));
        return this;
    }

    public TelegramBotKitBuilder UseWhen(Func<BotContext, bool> predicate, UpdateMiddleware middleware)
    {
        if (predicate is null) throw new ArgumentNullException(nameof(predicate));
        if (middleware is null) throw new ArgumentNullException(nameof(middleware));
        Services.AddSingleton<IMiddlewareConfigurator>(new DelegateMiddlewareConfigurator(p => p.UseWhen(predicate, middleware)));
        return this;
    }

    public TelegramBotKitBuilder Map<TPayload>(UpdateType updateType, Func<Update, TPayload?> extractor)
        where TPayload : class
    {
        if (extractor is null) throw new ArgumentNullException(nameof(extractor));
        Services.AddSingleton<IRegistryConfigurator>(new DelegateRegistryConfigurator(r => r.Map(updateType, extractor)));
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
}
