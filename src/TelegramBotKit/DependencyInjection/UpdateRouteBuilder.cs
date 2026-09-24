using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TelegramBotKit.Dispatching;
using TelegramBotKit.Middleware;

namespace TelegramBotKit.DependencyInjection;

/// <summary>Composes one route with middleware and at most one terminal.</summary>
public sealed class UpdateRouteBuilder<TPayload> where TPayload : class
{
    private readonly IServiceCollection _services;
    private readonly RouteRegistration<TPayload> _registration;
    internal UpdateRouteBuilder(IServiceCollection services, RouteRegistration<TPayload> registration)
        => (_services, _registration) = (services, registration);

    /// <summary>Assigns this route's single terminal, resolved from the update scope.</summary>
    /// <exception cref="TelegramBotKitRegistrationException">The route already has a terminal.</exception>
    public UpdateRouteBuilder<TPayload> HandleWith<THandler>(ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where THandler : class, IUpdatePayloadHandler<TPayload>
    {
        _registration.ValidateTerminal<THandler>();
        _services.TryAdd(new ServiceDescriptor(typeof(THandler), typeof(THandler), lifetime));
        _registration.SetTerminal<THandler>();
        return this;
    }

    /// <summary>
    /// Adds typed route-local middleware resolved from the per-update scope.
    /// The middleware receives the payload selected for this route and may short-circuit its terminal.
    /// </summary>
    public UpdateRouteBuilder<TPayload> Use<TMiddleware>(ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TMiddleware : class, IUpdateRouteMiddleware<TPayload>
    {
        _registration.EnsureMutable();
        _services.TryAdd(new ServiceDescriptor(typeof(TMiddleware), typeof(TMiddleware), lifetime));
        _registration.Use(static (ctx, next) =>
            ctx.BotContext.Services.GetRequiredService<TMiddleware>().InvokeAsync(ctx, next));
        return this;
    }

    /// <summary>
    /// Adds inline typed route-local middleware in registration order, outermost first.
    /// The delegate receives the selected payload and may omit the continuation to short-circuit the route.
    /// </summary>
    public UpdateRouteBuilder<TPayload> Use(
        Func<UpdateRouteContext<TPayload>, UpdateRouteDelegate<TPayload>, Task> middleware)
    {
        ArgumentNullException.ThrowIfNull(middleware);
        _registration.Use(middleware);
        return this;
    }

    /// <summary>
    /// Adds update-wide middleware to this route through the compatibility adapter.
    /// Prefer <see cref="Use{TMiddleware}(ServiceLifetime)"/> for typed route-local middleware.
    /// </summary>
    public UpdateRouteBuilder<TPayload> UseUpdateMiddleware<TMiddleware>(
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TMiddleware : class, IUpdateMiddleware
    {
        _registration.EnsureMutable();
        _services.TryAdd(new ServiceDescriptor(typeof(TMiddleware), typeof(TMiddleware), lifetime));
        _registration.UseUpdateMiddleware(static (ctx, next) =>
            ctx.Services.GetRequiredService<TMiddleware>().InvokeAsync(ctx, next));
        return this;
    }

    /// <summary>
    /// Adds an inline <see cref="IUpdateMiddleware"/>-shaped component to this route through the
    /// compatibility adapter. Prefer <see cref="Use(Func{UpdateRouteContext{TPayload}, UpdateRouteDelegate{TPayload}, Task})"/>
    /// when the middleware needs route payload typing.
    /// </summary>
    public UpdateRouteBuilder<TPayload> UseUpdateMiddleware(
        Func<BotContext, BotContextDelegate, Task> middleware)
    {
        ArgumentNullException.ThrowIfNull(middleware);
        _registration.UseUpdateMiddleware(middleware);
        return this;
    }
}
