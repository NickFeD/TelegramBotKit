using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramBotKit.Commands;
using TelegramBotKit.Dispatching;
using TelegramBotKit.Middleware;

namespace TelegramBotKit.DependencyInjection;

public sealed class TelegramBotKitBuilder
{
    private readonly List<Type> _middlewareTypes = new();

    internal IReadOnlyList<Type> MiddlewareTypes => _middlewareTypes;
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
}
