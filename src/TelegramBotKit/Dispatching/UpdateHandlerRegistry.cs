using Telegram.Bot.Types.Enums;
using TelegramBotKit.Middleware;
using Microsoft.Extensions.DependencyInjection;
using TelegramBotKit.Fallbacks;
using TelegramBotKit.Handlers;

namespace TelegramBotKit.Dispatching;

internal sealed class UpdateHandlerRegistry
{
    private readonly Dictionary<UpdateType, IRouteRegistration> _routes = new();
    private bool _frozen;

    internal RouteRegistration<TPayload> GetOrAdd<TPayload>(UpdateRoute<TPayload> descriptor)
        where TPayload : class
    {
        EnsureMutable();
        if (_routes.TryGetValue(descriptor.UpdateType, out var existing))
        {
            if (existing is RouteRegistration<TPayload> typed && ReferenceEquals(typed.Descriptor, descriptor))
                return typed;
            throw new TelegramBotKitRegistrationException(
                $"Update route '{descriptor.UpdateType}' already has a different descriptor. Reuse the original descriptor to compose its pipeline.");
        }
        var registration = new RouteRegistration<TPayload>(this, descriptor);
        _routes.Add(descriptor.UpdateType, registration);
        return registration;
    }

    internal bool Contains(UpdateType type) => _routes.ContainsKey(type);
    internal bool UsesDefaultMessageHandler =>
        _routes.TryGetValue(UpdateType.Message, out var route) && route.TerminalType == typeof(MessageUpdateHandler);
    public bool TryGetRoute(UpdateType type, out Func<BotContext, Task> route)
    {
        if (_routes.TryGetValue(type, out var registration))
        {
            route = registration.InvokeAsync;
            return true;
        }
        route = null!;
        return false;
    }

    public void Freeze() => _frozen = true;
    internal void EnsureMutable()
    {
        if (_frozen) throw new InvalidOperationException("UpdateHandlerRegistry is frozen. Configure it before bot starts.");
    }

    internal static Task FallbackAsync(BotContext ctx) =>
        ctx.Services.GetRequiredService<IDefaultUpdateHandler>().HandleAsync(ctx);
}

internal interface IRouteRegistration
{
    Type? TerminalType { get; }
    Task InvokeAsync(BotContext ctx);
}

internal sealed class RouteRegistration<TPayload>(UpdateHandlerRegistry registry, UpdateRoute<TPayload> descriptor)
    : IRouteRegistration where TPayload : class
{
    internal UpdateRoute<TPayload> Descriptor { get; } = descriptor;
    private readonly List<Func<BotContext, BotContextDelegate, Task>> _middleware = new();
    private TerminalDescriptor? _terminal;
    public Type? TerminalType => _terminal?.HandlerType;
    internal void EnsureMutable() => registry.EnsureMutable();

    internal void Use(Func<BotContext, BotContextDelegate, Task> middleware)
    {
        registry.EnsureMutable();
        _middleware.Add(middleware);
    }

    internal void ValidateTerminal<THandler>() where THandler : class, IUpdatePayloadHandler<TPayload>
    {
        registry.EnsureMutable();
        if (_terminal is not null)
            throw new TelegramBotKitRegistrationException(
                $"Update route '{Descriptor.UpdateType}' already has a terminal handler. " +
                $"Existing: {_terminal.HandlerType.FullName}. Attempted: {typeof(THandler).FullName}. " +
                "A route may contain multiple pipeline components, but only one terminal handler.");
    }

    internal void SetTerminal<THandler>() where THandler : class, IUpdatePayloadHandler<TPayload>
    {
        ValidateTerminal<THandler>();
        _terminal = new(typeof(THandler), static (payload, ctx) =>
            ctx.Services.GetRequiredService<THandler>().HandleAsync(payload, ctx));
    }

    public Task InvokeAsync(BotContext ctx)
    {
        var payload = Descriptor.PayloadSelector(ctx.Update);
        if (payload is null) return UpdateHandlerRegistry.FallbackAsync(ctx);
        BotContextDelegate app = context => _terminal is null
            ? UpdateHandlerRegistry.FallbackAsync(context)
            : _terminal.Invoke(payload, context);
        for (var i = _middleware.Count - 1; i >= 0; i--)
        {
            var middleware = _middleware[i];
            var next = app;
            app = context => middleware(context, next);
        }
        return app(ctx);
    }

    private sealed record TerminalDescriptor(Type HandlerType, Func<TPayload, BotContext, Task> Invoke);
}
