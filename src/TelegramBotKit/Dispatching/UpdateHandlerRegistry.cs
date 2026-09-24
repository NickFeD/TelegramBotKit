using Telegram.Bot.Types.Enums;
using TelegramBotKit.Middleware;
using Microsoft.Extensions.DependencyInjection;
using TelegramBotKit.Fallbacks;
using TelegramBotKit.Handlers;
using TelegramBotKit.Pipelines;

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

    public void Freeze()
    {
        if (_frozen) return;

        foreach (var route in _routes.Values)
            route.Compile();

        _frozen = true;
    }
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
    void Compile();
    Task InvokeAsync(BotContext ctx);
}

internal sealed class RouteRegistration<TPayload>(UpdateHandlerRegistry registry, UpdateRoute<TPayload> descriptor)
    : IRouteRegistration where TPayload : class
{
    internal UpdateRoute<TPayload> Descriptor { get; } = descriptor;
    private readonly List<IPipelineNode<UpdateRouteContext<TPayload>>> _nodes = new();
    private TerminalDescriptor? _terminal;
    private PipelineDelegate<UpdateRouteContext<TPayload>>? _app;
    public Type? TerminalType => _terminal?.HandlerType;
    internal void EnsureMutable() => registry.EnsureMutable();

    internal void Use(Func<BotContext, BotContextDelegate, Task> middleware)
    {
        registry.EnsureMutable();
        _nodes.Add(new DelegatePipelineNode<UpdateRouteContext<TPayload>>(
            (routeContext, next) => middleware(
                routeContext.BotContext,
                nextContext => next(routeContext.WithBotContext(nextContext)))));
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

    public void Compile()
    {
        var terminal = _terminal;
        _app = new Pipeline<UpdateRouteContext<TPayload>>(_nodes).Build(routeContext =>
            terminal is null
                ? UpdateHandlerRegistry.FallbackAsync(routeContext.BotContext)
                : terminal.Invoke(routeContext.Payload, routeContext.BotContext));
    }

    public Task InvokeAsync(BotContext ctx)
    {
        var payload = Descriptor.PayloadSelector(ctx.Update);
        if (payload is null) return UpdateHandlerRegistry.FallbackAsync(ctx);

        return (_app ?? throw new InvalidOperationException("Update route is not compiled."))(
            new UpdateRouteContext<TPayload>(ctx, payload));
    }

    private sealed record TerminalDescriptor(Type HandlerType, Func<TPayload, BotContext, Task> Invoke);
}
