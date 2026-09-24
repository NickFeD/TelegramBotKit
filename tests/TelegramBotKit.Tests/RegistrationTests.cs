using System.Collections.ObjectModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Telegram.Bot.Types;
using TelegramBotKit.DependencyInjection;
using TelegramBotKit.Dispatching;
using TelegramBotKit.Middleware;
using TelegramBotKit.Options;
using Xunit;
using static TelegramBotKit.Tests.UpdateRoutingTests;

namespace TelegramBotKit.Tests;

public sealed class RegistrationTests
{
    private static ServiceProvider Build(IServiceCollection services) => services.BuildServiceProvider(
        new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    private static TelegramBotKitBuilder Configure(IServiceCollection services)
    {
        services.AddLogging();
        services.AddSingleton<Trace>();
        return services.AddTelegramBotKit(o => o.Token = "123456:TEST_TOKEN");
    }

    [Fact]
    public async Task Repeated_registration_preserves_routes_and_all_middleware_in_one_runtime()
    {
        var services = new ServiceCollection();
        var first = Configure(services);
        var trace = new List<string>();
        first.UseMiddleware((Func<BotContext, BotContextDelegate, Task>)((ctx, next) => Around("global A", ctx, next)));
        first.Route(UpdateRoutes.Message).Use((ctx, next) => RouteAround("local A", ctx, next)).HandleWith<MessageHandler>();
        var second = services.AddTelegramBotKit(o => o.Polling.Limit = 42);
        second.UseMiddleware((Func<BotContext, BotContextDelegate, Task>)((ctx, next) => Around("global B", ctx, next)));
        second.Route(UpdateRoutes.Message).Use((ctx, next) => RouteAround("local B", ctx, next));
        second.Route(UpdateRoutes.EditedMessage).HandleWith<EditedHandler>();
        Assert.Same(first, second);
        Assert.Single(services, d => d.ServiceType == typeof(IUpdateDispatcher));
        await using var provider = Build(services);
        var options = provider.GetRequiredService<IOptions<TelegramBotKitOptions>>().Value;
        Assert.Equal("123456:TEST_TOKEN", options.Token);
        Assert.Equal(42, options.Polling.Limit);
        var dispatcher = provider.GetRequiredService<IUpdateDispatcher>();
        await dispatcher.DispatchAsync(new Update { Message = new Message() });
        Assert.Equal(new[] { "global A before", "global B before", "local A before", "local B before", "local B after", "local A after", "global B after", "global A after" }, trace);
        await dispatcher.DispatchAsync(new Update { EditedMessage = new Message() });
        Assert.Equal(new[] { "message", "edited" }, provider.GetRequiredService<Trace>().Events);

        async Task Around(string name, BotContext ctx, BotContextDelegate next)
        { trace.Add(name + " before"); await next(ctx); trace.Add(name + " after"); }
        async Task RouteAround(string name, UpdateRouteContext<Message> ctx,
            UpdateRouteDelegate<Message> next)
        { trace.Add(name + " before"); await next(ctx); trace.Add(name + " after"); }
    }

    [Fact]
    public void Duplicate_terminal_across_builders_fails_without_adding_attempted_service()
    {
        var services = new ServiceCollection();
        Configure(services).Route(UpdateRoutes.Message).HandleWith<MessageHandler>();
        var second = services.AddTelegramBotKit(_ => { });
        var before = services.ToArray();
        var error = Assert.Throws<TelegramBotKitRegistrationException>(() => second.Route(UpdateRoutes.Message).HandleWith<EditedHandler>());
        Assert.Contains("Message", error.Message);
        Assert.Contains(nameof(MessageHandler), error.Message);
        Assert.Contains(nameof(EditedHandler), error.Message);
        Assert.Equal(before, services.ToArray());
    }

    [Fact]
    public void Separate_service_collections_have_independent_configuration()
    {
        var first = Configure(new ServiceCollection());
        var second = Configure(new ServiceCollection());
        first.Route(UpdateRoutes.Message).HandleWith<MessageHandler>();
        second.Route(UpdateRoutes.Message).HandleWith<EditedHandler>();
        Assert.NotSame(first, second);
    }

    [Fact]
    public void Repeated_registration_after_freeze_does_not_partially_add_options()
    {
        var services = new ServiceCollection();
        Configure(services);
        using var provider = Build(services);
        provider.GetRequiredService<IUpdateDispatcher>();
        var before = services.ToArray();
        Assert.Throws<InvalidOperationException>(() => services.AddTelegramBotKit(_ => { }));
        Assert.Equal(before, services.ToArray());
    }

    [Fact]
    public async Task Readonly_DI_failure_does_not_occupy_terminal_and_existing_service_can_be_used()
    {
        var services = new ServiceCollection();
        var route = Configure(services).Route(UpdateRoutes.Message);
        services.AddScoped<EditedHandler>();
        services.MakeReadOnly();
        Assert.Throws<InvalidOperationException>(() => route.HandleWith<MessageHandler>());
        route.HandleWith<EditedHandler>();
        await using var provider = Build(services);
        await provider.GetRequiredService<IUpdateDispatcher>().DispatchAsync(new Update { Message = new Message() });
        Assert.Equal(new[] { "edited" }, provider.GetRequiredService<Trace>().Events);
    }

    [Fact]
    public async Task Failed_terminal_registration_can_be_retried_with_same_handler()
    {
        var services = new RejectingServices();
        var route = Configure(services).Route(UpdateRoutes.Message);
        services.Reject = typeof(MessageHandler);
        var before = services.ToArray();
        Assert.Throws<InvalidOperationException>(() => route.HandleWith<MessageHandler>());
        Assert.Equal(before, services.ToArray());
        services.Reject = null;
        route.HandleWith<MessageHandler>();
        await using var provider = Build(services);
        await provider.GetRequiredService<IUpdateDispatcher>().DispatchAsync(new Update { Message = new Message() });
        Assert.Equal(new[] { "message" }, provider.GetRequiredService<Trace>().Events);
    }

    [Fact]
    public async Task Failed_middleware_registration_does_not_change_pipeline_order()
    {
        var services = new RejectingServices();
        var route = Configure(services).Route(UpdateRoutes.Message);
        route.Use((ctx, next) => Wrap("A", ctx, next));
        services.Reject = typeof(RecordingMiddleware);
        var before = services.ToArray();
        Assert.Throws<InvalidOperationException>(() => route.Use<RecordingMiddleware>());
        Assert.Equal(before, services.ToArray());
        route.Use((ctx, next) => Wrap("B", ctx, next));
        services.Reject = null;
        route.Use<RecordingMiddleware>().HandleWith<MessageHandler>();
        await using var provider = Build(services);
        await provider.GetRequiredService<IUpdateDispatcher>().DispatchAsync(new Update { Message = new Message() });
        Assert.Equal(new[] { "A before", "B before", "class before", "message", "class after", "B after", "A after" }, provider.GetRequiredService<Trace>().Events);
    }

    [Fact]
    public async Task Readonly_DI_failure_does_not_append_unresolvable_middleware()
    {
        var services = new ServiceCollection();
        var route = Configure(services).Route(UpdateRoutes.Message).HandleWith<MessageHandler>();
        services.MakeReadOnly();
        Assert.Throws<InvalidOperationException>(() => route.Use<RecordingMiddleware>());
        await using var provider = Build(services);
        await provider.GetRequiredService<IUpdateDispatcher>().DispatchAsync(new Update { Message = new Message() });
        Assert.Equal(new[] { "message" }, provider.GetRequiredService<Trace>().Events);
    }

    private static async Task Wrap(string name, UpdateRouteContext<Message> ctx,
        UpdateRouteDelegate<Message> next)
    {
        var trace = ctx.BotContext.Services.GetRequiredService<Trace>();
        trace.Events.Add(name + " before"); await next(ctx); trace.Events.Add(name + " after");
    }
    public sealed class RecordingMiddleware : IUpdateRouteMiddleware<Message>
    {
        public Task InvokeAsync(UpdateRouteContext<Message> ctx, UpdateRouteDelegate<Message> next) =>
            Wrap("class", ctx, next);
    }
    private sealed class RejectingServices : Collection<ServiceDescriptor>, IServiceCollection
    {
        public Type? Reject { get; set; }
        protected override void InsertItem(int index, ServiceDescriptor item)
        {
            if (item.ServiceType == Reject) throw new InvalidOperationException("Simulated DI failure");
            base.InsertItem(index, item);
        }
    }
}
