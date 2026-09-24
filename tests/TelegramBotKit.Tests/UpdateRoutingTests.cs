using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramBotKit.Commands;
using TelegramBotKit.Conversations;
using TelegramBotKit.DependencyInjection;
using TelegramBotKit.Dispatching;
using TelegramBotKit.Fallbacks;
using TelegramBotKit.Middleware;
using Xunit;

namespace TelegramBotKit.Tests;

public sealed class UpdateRoutingTests
{
    private static (ServiceCollection Services, TelegramBotKitBuilder Bot, Trace Trace) Setup()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var trace = new Trace();
        services.AddSingleton(trace);
        services.AddScoped<IDefaultUpdateHandler, Fallback>();
        services.AddSingleton<IDefaultMessageHandler, MessageFallback>();
        services.AddSingleton<IDefaultCallbackHandler, CallbackFallback>();
        var bot = services.AddTelegramBotKit(options => options.Token = "123456:TEST_TOKEN");
        return (services, bot, trace);
    }

    private static ServiceProvider Build(ServiceCollection services) =>
        services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

    private static Message Message(string text = "hello") => new()
    {
        Text = text, Chat = new Chat { Id = 123 }, From = new User { Id = 456, FirstName = "Test" }
    };

    [Fact]
    public async Task Same_payload_routes_have_independent_terminals_and_pipelines()
    {
        var (services, bot, trace) = Setup();
        bot.Route(UpdateRoutes.Message).Use((ctx, next) => { trace.Events.Add("message route"); return next(ctx); })
            .HandleWith<MessageHandler>();
        bot.Route(UpdateRoutes.EditedMessage)
            .Use((ctx, next) => { trace.Events.Add("edited route"); return next(ctx); })
            .HandleWith<EditedHandler>();
        // Old interface registrations cannot affect ownership.
        services.AddScoped<IUpdatePayloadHandler<Message>, ThrowingHandler>();
        await using var provider = Build(services);
        var dispatcher = provider.GetRequiredService<IUpdateDispatcher>();
        var original = Message();
        var edited = Message("edited");
        await dispatcher.DispatchAsync(new Update { Message = original });
        await dispatcher.DispatchAsync(new Update { EditedMessage = edited });
        Assert.Equal(new[] { "message route", "message", "edited route", "edited" }, trace.Events);
        Assert.Same(edited, trace.Payload);
    }

    [Fact]
    public async Task Typed_route_middleware_receives_exact_payload_instance()
    {
        var (services, bot, trace) = Setup();
        bot.Route(UpdateRoutes.Message)
            .Use<PayloadCapturingMiddleware>()
            .HandleWith<MessageHandler>();
        await using var provider = Build(services);
        var payload = Message();

        await provider.GetRequiredService<IUpdateDispatcher>()
            .DispatchAsync(new Update { Message = payload });

        Assert.Same(payload, trace.MiddlewarePayload);
        Assert.Same(payload, trace.Payload);
    }

    [Fact]
    public void Typed_route_context_has_minimal_read_only_public_surface()
    {
        var type = typeof(UpdateRouteContext<Message>);
        var properties = type.GetProperties(
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.DeclaredOnly);

        Assert.Empty(type.GetConstructors());
        Assert.Equal(new[] { "BotContext", "Payload" }, properties.Select(p => p.Name).OrderBy(n => n));
        Assert.All(properties, property => Assert.False(property.CanWrite));
    }

    [Fact]
    public void Duplicate_terminal_fails_during_configuration_with_both_names()
    {
        var (_, bot, _) = Setup();
        bot.Route(UpdateRoutes.Message).HandleWith<MessageHandler>();
        var error = Assert.Throws<TelegramBotKitRegistrationException>(() =>
            bot.Route(UpdateRoutes.Message).HandleWith<EditedHandler>());
        Assert.Contains("Message", error.Message);
        Assert.Contains(nameof(MessageHandler), error.Message);
        Assert.Contains(nameof(EditedHandler), error.Message);
    }

    [Fact]
    public void Same_terminal_twice_is_also_invalid()
    {
        var (_, bot, _) = Setup();
        var route = bot.Route(UpdateRoutes.Message).HandleWith<MessageHandler>();
        Assert.Throws<TelegramBotKitRegistrationException>(() => route.HandleWith<MessageHandler>());
    }

    [Fact]
    public void Future_enum_value_can_be_registered_without_a_catalog_entry()
    {
        var (_, bot, _) = Setup();
        var future = UpdateRoute.Create((UpdateType)int.MaxValue, update => update.Message);
        bot.Route(future).HandleWith<MessageHandler>();
    }

    [Fact]
    public void Conflicting_descriptor_cannot_replace_extractor_or_payload_type()
    {
        var (_, bot, _) = Setup();
        bot.Route(UpdateRoutes.Message);
        Assert.Throws<TelegramBotKitRegistrationException>(() =>
            bot.Route(UpdateRoute.Create(UpdateType.Message, u => u.EditedMessage)));
        Assert.Throws<TelegramBotKitRegistrationException>(() =>
            bot.Route(UpdateRoute.Create(UpdateType.Message, u => u.CallbackQuery)));
    }

    [Fact]
    public void Handler_generic_constraint_rejects_wrong_payload()
    {
        var method = typeof(UpdateRouteBuilder<Message>).GetMethod("HandleWith")!;
        Assert.NotNull(method.MakeGenericMethod(typeof(MessageHandler)));
        Assert.Throws<ArgumentException>(() => method.MakeGenericMethod(typeof(QueryHandler)));
    }

    [Fact]
    public void Route_middleware_generic_constraint_rejects_wrong_payload()
    {
        var method = typeof(UpdateRouteBuilder<Message>).GetMethods()
            .Single(candidate => candidate.Name == "Use" && candidate.IsGenericMethodDefinition);
        Assert.NotNull(method.MakeGenericMethod(typeof(MessageRouteMiddleware)));
        Assert.Throws<ArgumentException>(() => method.MakeGenericMethod(typeof(QueryRouteMiddleware)));
    }

    [Fact]
    public async Task Custom_descriptor_extracts_payload_and_uses_normal_pipelines()
    {
        var (services, bot, trace) = Setup();
        UpdateRoute<Message> custom = UpdateRoute.Create(UpdateType.EditedMessage, u => u.EditedMessage);
        bot.UseMiddleware((Func<BotContext, BotContextDelegate, Task>)((ctx, next) => Around(trace, "global", ctx, next)));
        bot.Route(custom)
            .Use<MessageRouteMiddleware>()
            .Use((ctx, next) => Around(trace, "route", ctx, next))
            .HandleWith<EditedHandler>();
        await using var provider = Build(services);
        var payload = Message();
        await provider.GetRequiredService<IUpdateDispatcher>().DispatchAsync(new Update { EditedMessage = payload });
        Assert.Same(payload, trace.Payload);
        Assert.Equal(new[] { "global before", "route before", "edited", "route after", "global after" }, trace.Events);
    }

    [Fact]
    public async Task Custom_route_does_not_require_catalog_membership()
    {
        var (services, bot, trace) = Setup();
        var payload = Message();
        bot.Route(UpdateRoute.Create(UpdateType.Unknown, _ => payload)).HandleWith<MessageHandler>();
        await using var provider = Build(services);
        await provider.GetRequiredService<IUpdateDispatcher>().DispatchAsync(new Update());
        Assert.Same(payload, trace.Payload);
        Assert.Equal(new[] { "message" }, trace.Events);
    }

    [Fact]
    public async Task Repeated_route_configuration_adds_nested_middleware_in_order()
    {
        var (services, bot, trace) = Setup();
        bot.UseMiddleware((Func<BotContext, BotContextDelegate, Task>)((ctx, next) =>
            Around(trace, "global", ctx, next)));
        bot.Route(UpdateRoutes.Message).Use((ctx, next) => Around(trace, "A", ctx, next));
        bot.Route(UpdateRoutes.Message).Use((ctx, next) => Around(trace, "B", ctx, next)).HandleWith<MessageHandler>();
        await using var provider = Build(services);
        await provider.GetRequiredService<IUpdateDispatcher>().DispatchAsync(new Update { Message = Message() });
        Assert.Equal(
            new[] { "global before", "A before", "B before", "message", "B after", "A after", "global after" },
            trace.Events);
    }

    [Fact]
    public async Task Route_middleware_forwards_replacement_context_with_original_payload()
    {
        var (services, bot, trace) = Setup();
        var originalPayload = Message("original");
        var replacementUpdate = new Update { Message = Message("replacement") };
        bot.Route(UpdateRoutes.Message).UseUpdateMiddleware((ctx, next) => next(new BotContext(
            replacementUpdate,
            ctx.BotClient,
            ctx.Sender,
            ctx.Services,
            ctx.CancellationToken))).HandleWith<MessageHandler>();
        await using var provider = Build(services);

        await provider.GetRequiredService<IUpdateDispatcher>()
            .DispatchAsync(new Update { Message = originalPayload });

        Assert.Same(originalPayload, trace.Payload);
        Assert.Same(replacementUpdate, trace.ContextUpdate);
    }

    [Fact]
    public async Task Update_middleware_can_be_attached_to_route_through_named_adapter()
    {
        var (services, bot, trace) = Setup();
        bot.Route(UpdateRoutes.Message)
            .UseUpdateMiddleware<LegacyRouteMiddleware>()
            .HandleWith<MessageHandler>();
        await using var provider = Build(services);

        await provider.GetRequiredService<IUpdateDispatcher>()
            .DispatchAsync(new Update { Message = Message() });

        Assert.Equal(new[] { "legacy before", "message", "legacy after" }, trace.Events);
    }

    [Fact]
    public async Task Route_payload_is_extracted_once()
    {
        var (services, bot, _) = Setup();
        var extractions = 0;
        var route = UpdateRoute.Create(UpdateType.EditedMessage, (Update update) =>
        {
            extractions++;
            return update.EditedMessage;
        });
        bot.Route(route).Use((ctx, next) => next(ctx)).HandleWith<EditedHandler>();
        await using var provider = Build(services);

        await provider.GetRequiredService<IUpdateDispatcher>()
            .DispatchAsync(new Update { EditedMessage = Message() });

        Assert.Equal(1, extractions);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Middleware_can_short_circuit(bool global)
    {
        var (services, bot, trace) = Setup();
        Func<BotContext, BotContextDelegate, Task> globalStop = (_, _) =>
        {
            trace.Events.Add("stop");
            return Task.CompletedTask;
        };
        if (global) bot.UseMiddleware(globalStop);
        var route = bot.Route(UpdateRoutes.Message);
        if (!global) route.Use((_, _) =>
        {
            trace.Events.Add("stop");
            return Task.CompletedTask;
        });
        route.HandleWith<MessageHandler>();
        await using var provider = Build(services);
        await provider.GetRequiredService<IUpdateDispatcher>().DispatchAsync(new Update { Message = Message() });
        Assert.Equal(new[] { "stop" }, trace.Events);
    }

    [Fact]
    public async Task Handler_exception_unwinds_both_pipelines_and_propagates()
    {
        var (services, bot, trace) = Setup();
        bot.UseMiddleware((Func<BotContext, BotContextDelegate, Task>)((ctx, next) => Around(trace, "global", ctx, next)));
        bot.Route(UpdateRoutes.Message).Use((ctx, next) => Around(trace, "route", ctx, next)).HandleWith<ThrowingHandler>();
        await using var provider = Build(services);
        await Assert.ThrowsAsync<TestException>(() => provider.GetRequiredService<IUpdateDispatcher>()
            .DispatchAsync(new Update { Message = Message() }));
        Assert.Equal(new[] { "global before", "route before", "route after", "global after" }, trace.Events);
    }

    [Theory]
    [InlineData("unregistered")]
    [InlineData("empty")]
    [InlineData("middleware")]
    [InlineData("null")]
    public async Task Missing_terminal_or_payload_uses_update_fallback(string kind)
    {
        var (services, bot, trace) = Setup();
        if (kind == "empty") bot.Route(UpdateRoutes.EditedMessage);
        if (kind == "middleware") bot.Route(UpdateRoutes.EditedMessage).Use((ctx, next) => Around(trace, "route", ctx, next));
        if (kind == "null") bot.Route(UpdateRoute.Create<Message>(UpdateType.EditedMessage, _ => null)).HandleWith<ThrowingHandler>();
        await using var provider = Build(services);
        await provider.GetRequiredService<IUpdateDispatcher>().DispatchAsync(new Update { EditedMessage = Message() });
        Assert.Equal(kind == "middleware" ? new[] { "route before", "fallback", "route after" } : new[] { "fallback" }, trace.Events);
    }

    [Fact]
    public async Task Null_payload_skips_route_pipeline_inside_global_pipeline()
    {
        var (services, bot, trace) = Setup();
        bot.UseMiddleware((Func<BotContext, BotContextDelegate, Task>)((ctx, next) =>
            Around(trace, "global", ctx, next)));
        bot.Route(UpdateRoute.Create<Message>(UpdateType.EditedMessage, _ => null))
            .Use((ctx, next) => Around(trace, "route", ctx, next))
            .HandleWith<ThrowingHandler>();
        await using var provider = Build(services);

        await provider.GetRequiredService<IUpdateDispatcher>()
            .DispatchAsync(new Update { EditedMessage = Message() });

        Assert.Equal(new[] { "global before", "fallback", "global after" }, trace.Events);
    }

    [Fact]
    public async Task Route_without_handler_runs_middleware_around_fallback_inside_global_pipeline()
    {
        var (services, bot, trace) = Setup();
        bot.UseMiddleware((Func<BotContext, BotContextDelegate, Task>)((ctx, next) =>
            Around(trace, "global", ctx, next)));
        bot.Route(UpdateRoutes.EditedMessage)
            .Use((ctx, next) => Around(trace, "route", ctx, next));
        await using var provider = Build(services);

        await provider.GetRequiredService<IUpdateDispatcher>()
            .DispatchAsync(new Update { EditedMessage = Message() });

        Assert.Equal(
            new[] { "global before", "route before", "fallback", "route after", "global after" },
            trace.Events);
    }

    [Fact]
    public async Task Global_and_route_middleware_share_update_scope_disposed_after_unwind()
    {
        var (services, bot, trace) = Setup();
        services.AddScoped<ScopedProbe>();
        bot.UseMiddleware<ScopedMiddleware>();
        bot.Route(UpdateRoutes.Message).Use<ScopedRouteMiddleware>().HandleWith<ScopedHandler>();
        await using var provider = Build(services);
        var dispatcher = provider.GetRequiredService<IUpdateDispatcher>();
        await dispatcher.DispatchAsync(new Update { Message = Message() });
        await dispatcher.DispatchAsync(new Update { Message = Message() });
        Assert.Equal(6, trace.Scopes.Count);
        Assert.Equal(trace.Scopes[0], trace.Scopes[1]);
        Assert.Equal(trace.Scopes[1], trace.Scopes[2]);
        Assert.NotEqual(trace.Scopes[0], trace.Scopes[3]);
        Assert.Equal(new[] { "scoped handler", "unwind", "unwind", "disposed", "scoped handler", "unwind", "unwind", "disposed" }, trace.Events);
    }

    [Fact]
    public async Task Failed_terminal_still_disposes_scope_after_middleware_unwind()
    {
        var (services, bot, trace) = Setup();
        services.AddScoped<ScopedProbe>();
        bot.Route(UpdateRoutes.Message).Use(async (ctx, next) =>
        {
            var probe = ctx.BotContext.Services.GetRequiredService<ScopedProbe>();
            try { await next(ctx); }
            finally { Assert.False(probe.Disposed); trace.Events.Add("unwind"); }
        }).HandleWith<ThrowingHandler>();
        await using var provider = Build(services);
        await Assert.ThrowsAsync<TestException>(() => provider.GetRequiredService<IUpdateDispatcher>()
            .DispatchAsync(new Update { Message = Message() }));
        Assert.Equal(new[] { "unwind", "disposed" }, trace.Events);
    }

    [Fact]
    public async Task Default_message_flow_keeps_conversation_precedence_and_commands()
    {
        var (services, _, trace) = Setup();
        services.AddMessageCommand<StartCommand>("/start");
        services.AddTextCommand<TextCommand>(new[] { "hello" });
        await using var provider = Build(services);
        var wait = provider.GetRequiredService<WaitForUserResponse>();
        var pending = wait.WaitAsync(123, 456, TimeSpan.FromSeconds(5));
        var response = Message("/start");
        var dispatcher = provider.GetRequiredService<IUpdateDispatcher>();
        await dispatcher.DispatchAsync(new Update { Message = response });
        Assert.Same(response, await pending);
        Assert.Empty(trace.Events);
        await dispatcher.DispatchAsync(new Update { Message = Message("/start") });
        await dispatcher.DispatchAsync(new Update { Message = Message("hello") });
        await dispatcher.DispatchAsync(new Update { Message = Message("other") });
        await dispatcher.DispatchAsync(new Update { EditedMessage = Message("/start") });
        Assert.Equal(new[] { "command", "text", "message fallback", "fallback" }, trace.Events);
    }

    [Fact]
    public async Task Default_callback_flow_keeps_commands_and_fallback()
    {
        var (services, _, trace) = Setup();
        services.AddCallbackCommand<CallbackCommand>("test");
        await using var provider = Build(services);
        var dispatcher = provider.GetRequiredService<IUpdateDispatcher>();
        await dispatcher.DispatchAsync(new Update { CallbackQuery = new CallbackQuery { Data = "test arg" } });
        await dispatcher.DispatchAsync(new Update { CallbackQuery = new CallbackQuery { Data = "unknown" } });
        Assert.Equal(new[] { "callback arg", "callback fallback" }, trace.Events);
    }

    [Fact]
    public void Registry_is_frozen_when_dispatcher_is_resolved()
    {
        var (services, bot, _) = Setup();
        var route = bot.Route(UpdateRoutes.Message);
        using var provider = Build(services);
        provider.GetRequiredService<IUpdateDispatcher>();
        Assert.Throws<InvalidOperationException>(() => route.HandleWith<MessageHandler>());
        Assert.Throws<InvalidOperationException>(() => route.Use((_, _) => Task.CompletedTask));
        Assert.Throws<InvalidOperationException>(() => bot.Route(UpdateRoutes.EditedMessage));
    }

    [Fact]
    public void Catalog_covers_every_typed_update_property_with_correct_selector()
    {
        var properties = typeof(Update).GetProperties().Where(p => p.Name != "Type" && p.Name != "Id").ToArray();
        Assert.Equal(properties.Length, typeof(UpdateRoutes).GetProperties().Length);
        foreach (var property in properties)
        {
            var descriptor = typeof(UpdateRoutes).GetProperty(property.Name)!.GetValue(null)!;
            Assert.Equal(property.PropertyType, descriptor.GetType().GenericTypeArguments.Single());
            var update = new Update();
            var payload = Activator.CreateInstance(property.PropertyType);
            property.SetValue(update, payload);
            Assert.Equal(update.Type, descriptor.GetType().GetProperty("UpdateType")!.GetValue(descriptor));
            var selector = (Delegate)descriptor.GetType().GetProperty("PayloadSelector", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(descriptor)!;
            Assert.Same(payload, selector.DynamicInvoke(update));
        }
    }

    private static async Task Around(Trace trace, string name, BotContext ctx, BotContextDelegate next)
    {
        trace.Events.Add(name + " before");
        try { await next(ctx); }
        finally { trace.Events.Add(name + " after"); }
    }

    private static async Task Around<TPayload>(Trace trace, string name,
        UpdateRouteContext<TPayload> ctx, UpdateRouteDelegate<TPayload> next)
        where TPayload : class
    {
        trace.Events.Add(name + " before");
        try { await next(ctx); }
        finally { trace.Events.Add(name + " after"); }
    }

    public sealed class Trace
    {
        public List<string> Events { get; } = new();
        public List<Guid> Scopes { get; } = new();
        public Message? Payload { get; set; }
        public Message? MiddlewarePayload { get; set; }
        public Update? ContextUpdate { get; set; }
    }
    public sealed class MessageHandler(Trace trace) : IUpdatePayloadHandler<Message>
    {
        public Task HandleAsync(Message payload, BotContext ctx)
        {
            trace.Payload = payload;
            trace.ContextUpdate = ctx.Update;
            trace.Events.Add("message");
            return Task.CompletedTask;
        }
    }
    public sealed class EditedHandler(Trace trace) : IUpdatePayloadHandler<Message>
    {
        public Task HandleAsync(Message payload, BotContext ctx) { trace.Payload = payload; trace.Events.Add("edited"); return Task.CompletedTask; }
    }
    public sealed class QueryHandler : IUpdatePayloadHandler<CallbackQuery>
    {
        public Task HandleAsync(CallbackQuery payload, BotContext ctx) => Task.CompletedTask;
    }
    public sealed class MessageRouteMiddleware : IUpdateRouteMiddleware<Message>
    {
        public Task InvokeAsync(UpdateRouteContext<Message> context, UpdateRouteDelegate<Message> next) =>
            next(context);
    }
    public sealed class PayloadCapturingMiddleware(Trace trace) : IUpdateRouteMiddleware<Message>
    {
        public Task InvokeAsync(UpdateRouteContext<Message> context,
            UpdateRouteDelegate<Message> next)
        {
            trace.MiddlewarePayload = context.Payload;
            return next(context);
        }
    }
    public sealed class QueryRouteMiddleware : IUpdateRouteMiddleware<CallbackQuery>
    {
        public Task InvokeAsync(UpdateRouteContext<CallbackQuery> context,
            UpdateRouteDelegate<CallbackQuery> next) => next(context);
    }
    public sealed class TestException : Exception { }
    public sealed class ThrowingHandler : IUpdatePayloadHandler<Message>
    {
        public Task HandleAsync(Message payload, BotContext ctx) => Task.FromException(new TestException());
    }
    public sealed class Fallback(Trace trace) : IDefaultUpdateHandler
    {
        public Task HandleAsync(BotContext ctx) { trace.Events.Add("fallback"); return Task.CompletedTask; }
    }
    public sealed class MessageFallback(Trace trace) : IDefaultMessageHandler
    {
        public Task HandleAsync(Message message, BotContext ctx) { trace.Events.Add("message fallback"); return Task.CompletedTask; }
    }
    public sealed class CallbackFallback(Trace trace) : IDefaultCallbackHandler
    {
        public Task HandleAsync(CallbackQuery query, BotContext ctx) { trace.Events.Add("callback fallback"); return Task.CompletedTask; }
    }
    public sealed class StartCommand(Trace trace) : IMessageCommand
    {
        public Task HandleAsync(Message message, BotContext ctx) { trace.Events.Add("command"); return Task.CompletedTask; }
    }
    public sealed class TextCommand(Trace trace) : ITextCommand
    {
        public Task HandleAsync(Message message, BotContext ctx) { trace.Events.Add("text"); return Task.CompletedTask; }
    }
    public sealed class CallbackCommand(Trace trace) : ICallbackCommand
    {
        public Task HandleAsync(CallbackQuery query, string[] args, BotContext ctx) { trace.Events.Add("callback " + args.Single()); return Task.CompletedTask; }
    }
    public sealed class ScopedProbe(Trace trace) : IAsyncDisposable
    {
        public Guid Id { get; } = Guid.NewGuid();
        public bool Disposed { get; private set; }
        public ValueTask DisposeAsync() { Disposed = true; trace.Events.Add("disposed"); return ValueTask.CompletedTask; }
    }
    public sealed class ScopedMiddleware(Trace trace, ScopedProbe probe) : IUpdateMiddleware
    {
        public async Task InvokeAsync(BotContext ctx, BotContextDelegate next)
        {
            trace.Scopes.Add(probe.Id);
            await next(ctx);
            Assert.False(probe.Disposed);
            trace.Events.Add("unwind");
        }
    }
    public sealed class LegacyRouteMiddleware(Trace trace) : IUpdateMiddleware
    {
        public async Task InvokeAsync(BotContext context, BotContextDelegate next)
        {
            trace.Events.Add("legacy before");
            await next(context);
            trace.Events.Add("legacy after");
        }
    }
    public sealed class ScopedRouteMiddleware(Trace trace, ScopedProbe probe)
        : IUpdateRouteMiddleware<Message>
    {
        public async Task InvokeAsync(UpdateRouteContext<Message> context,
            UpdateRouteDelegate<Message> next)
        {
            trace.Scopes.Add(probe.Id);
            await next(context);
            Assert.False(probe.Disposed);
            trace.Events.Add("unwind");
        }
    }
    public sealed class ScopedHandler(Trace trace, ScopedProbe probe) : IUpdatePayloadHandler<Message>
    {
        public Task HandleAsync(Message payload, BotContext ctx)
        {
            trace.Scopes.Add(probe.Id); trace.Events.Add("scoped handler"); return Task.CompletedTask;
        }
    }
}
