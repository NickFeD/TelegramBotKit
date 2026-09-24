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

    /// <summary>Adds middleware resolved from the per-update scope.</summary>
    public UpdateRouteBuilder<TPayload> Use<TMiddleware>(ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TMiddleware : class, IUpdateMiddleware
    {
        _registration.EnsureMutable();
        _services.TryAdd(new ServiceDescriptor(typeof(TMiddleware), typeof(TMiddleware), lifetime));
        _registration.Use(static (ctx, next) => ctx.Services.GetRequiredService<TMiddleware>().InvokeAsync(ctx, next));
        return this;
    }

    /// <summary>Adds inline route middleware in registration order, outermost first.</summary>
    public UpdateRouteBuilder<TPayload> Use(Func<BotContext, BotContextDelegate, Task> middleware)
    {
        ArgumentNullException.ThrowIfNull(middleware);
        _registration.Use(middleware);
        return this;
    }
}
