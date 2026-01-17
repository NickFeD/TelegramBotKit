using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using System.Reflection;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramBotKit.Commands;
using TelegramBotKit.Conversations;
using TelegramBotKit.Dispatching;
using TelegramBotKit.Fallbacks;
using TelegramBotKit.Handlers;
using TelegramBotKit.Messaging;
using TelegramBotKit.Middleware;
using TelegramBotKit.Options;
using TelegramBotKit.Routing;

namespace TelegramBotKit.DependencyInjection;


/// <summary>
/// Provides a telegram bot kit service collection extensions.
/// </summary>
public static partial class TelegramBotKitServiceCollectionExtensions
{
    /// <summary>
    /// Adds the telegram bot kit.
    /// </summary>
    public static TelegramBotKitBuilder AddTelegramBotKit(
        this IServiceCollection services,
        Action<TelegramBotKitOptions> configure)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));
        if (configure is null) throw new ArgumentNullException(nameof(configure));

        services.AddOptions<TelegramBotKitOptions>()
            .Configure(configure)
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<TelegramBotKitOptions>, TelegramBotKitOptionsValidator>();

        services.AddSingleton<ITelegramBotClient>(sp =>
        {
            var opt = sp.GetRequiredService<IOptions<TelegramBotKitOptions>>().Value;
            var clientOptions = new TelegramBotClientOptions(opt.Token);
            return new TelegramBotClient(clientOptions);
        });

        services.TryAddSingleton<MessageSender>();
        services.TryAddSingleton<IMessageSender>(sp => sp.GetRequiredService<MessageSender>());

        services.AddSingleton<WaitForUserResponse>();

        services.AddScoped<CommandRouter>();
        services.TryAddSingleton<CommandRegistry>();

        services.AddScoped<IUpdatePayloadHandler<Message>, MessageUpdateHandler>();
        services.AddScoped<IUpdatePayloadHandler<CallbackQuery>, CallbackQueryUpdateHandler>();

        services.TryAddSingleton<IDefaultUpdateHandler, NoopDefaultUpdateHandler>();
        services.TryAddSingleton<IDefaultMessageHandler, NoopDefaultMessageHandler>();
        services.TryAddSingleton<IDefaultCallbackHandler, NoopDefaultCallbackHandler>();

        var builder = new TelegramBotKitBuilder(services);

        services.AddSingleton(sp => new MiddlewarePipeline(sp, builder.MiddlewareFactories));

        services.AddSingleton<UpdateHandlerRegistry>(sp =>
        {
            var reg = new UpdateHandlerRegistry();

            MapDefaultUpdatePayloads(reg);

            foreach (var add in builder.RegistryActions)
                add(reg);

            reg.Freeze();
            return reg;
        });

        services.AddSingleton<IUpdateDispatcher, UpdateRouter>();

        return builder;
    }


    /// <summary>
    /// Adds the update handler.
    /// </summary>
    public static IServiceCollection AddUpdateHandler<TPayload, THandler>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TPayload : class
        where THandler : class, IUpdatePayloadHandler<TPayload>
    {
        if (services is null) throw new ArgumentNullException(nameof(services));

        services.Add(new ServiceDescriptor(typeof(IUpdatePayloadHandler<TPayload>), typeof(THandler), lifetime));
        return services;
    }


/// <summary>
/// Adds the telegram bot kit queued message sender.
/// </summary>
public static IServiceCollection AddTelegramBotKitQueuedMessageSender(
    this IServiceCollection services,
    Action<QueuedMessageSenderOptions>? configure = null)
{
    if (services is null) throw new ArgumentNullException(nameof(services));

    var opt = services.AddOptions<QueuedMessageSenderOptions>();
    if (configure is not null)
        opt.Configure(configure);

    services.TryAddSingleton<MessageSender>();

    services.TryAddSingleton<QueuedMessageSender>();

    services.Replace(ServiceDescriptor.Singleton<IMessageSender>(sp => sp.GetRequiredService<QueuedMessageSender>()));

    return services;
}

    private static void MapDefaultUpdatePayloads(UpdateHandlerRegistry reg)
    {
        // Minimal defaults that cover the built-in handlers registered by AddTelegramBotKit.
        // Additional payloads can be mapped via TelegramBotKitBuilder.Map(...).
        reg.Map<Message>(UpdateType.Message, static u => u.Message);
        reg.Map<CallbackQuery>(UpdateType.CallbackQuery, static u => u.CallbackQuery);
    }



    /// <summary>
    /// Registers a slash-message command (e.g. "/start") without assembly scanning (AOT-friendly).
    /// The command instance is resolved from the per-update scope (<see cref="BotContext.Services"/>).
    /// </summary>
    /// <typeparam name="TCommand">Concrete command type.</typeparam>
    /// <param name="services">Service collection.</param>
    /// <param name="command">Slash command string ("/start").</param>
    /// <param name="lifetime">DI lifetime for the command type. Defaults to <see cref="ServiceLifetime.Transient"/>.</param>
    public static IServiceCollection AddMessageCommand<TCommand>(
        this IServiceCollection services,
        string command,
        ServiceLifetime lifetime = ServiceLifetime.Transient)
        where TCommand : class, IMessageCommand
    {
        if (services is null) throw new ArgumentNullException(nameof(services));
        if (string.IsNullOrWhiteSpace(command)) throw new ArgumentException("Command is required.", nameof(command));

        services.Add(new ServiceDescriptor(typeof(TCommand), typeof(TCommand), lifetime));

        services.AddSingleton(new MessageCommandDescriptor(
            command,
            static (message, ctx) =>
            {
                var handler = ctx.Services.GetRequiredService<TCommand>();
                return new ValueTask(handler.HandleAsync(message, ctx));
            }));

        return services;
    }

    /// <summary>
    /// Registers an exact text-trigger command without assembly scanning (AOT-friendly).
    /// The command instance is resolved from the per-update scope (<see cref="BotContext.Services"/>).
    /// </summary>
    /// <typeparam name="TCommand">Concrete command type.</typeparam>
    /// <param name="services">Service collection.</param>
    /// <param name="triggers">Text triggers (exact match).</param>
    /// <param name="ignoreCase">Whether triggers should match ignoring case.</param>
    /// <param name="lifetime">DI lifetime for the command type. Defaults to <see cref="ServiceLifetime.Transient"/>.</param>
    public static IServiceCollection AddTextCommand<TCommand>(
        this IServiceCollection services,
        IReadOnlyList<string> triggers,
        bool ignoreCase = true,
        ServiceLifetime lifetime = ServiceLifetime.Transient)
        where TCommand : class, ITextCommand
    {
        if (services is null) throw new ArgumentNullException(nameof(services));
        if (triggers is null) throw new ArgumentNullException(nameof(triggers));
        if (triggers.Count == 0) throw new ArgumentException("At least one trigger is required.", nameof(triggers));

        services.Add(new ServiceDescriptor(typeof(TCommand), typeof(TCommand), lifetime));

        services.AddSingleton(new TextCommandDescriptor(
            triggers,
            ignoreCase,
            static (message, ctx) =>
            {
                var handler = ctx.Services.GetRequiredService<TCommand>();
                return new ValueTask(handler.HandleAsync(message, ctx));
            }));

        return services;
    }

    /// <summary>
    /// Registers a callback command without assembly scanning (AOT-friendly).
    /// The command instance is resolved from the per-update scope (<see cref="BotContext.Services"/>).
    /// </summary>
    /// <typeparam name="TCommand">Concrete command type.</typeparam>
    /// <param name="services">Service collection.</param>
    /// <param name="key">Callback key.</param>
    /// <param name="lifetime">DI lifetime for the command type. Defaults to <see cref="ServiceLifetime.Transient"/>.</param>
    public static IServiceCollection AddCallbackCommand<TCommand>(
        this IServiceCollection services,
        string key,
        ServiceLifetime lifetime = ServiceLifetime.Transient)
        where TCommand : class, ICallbackCommand
    {
        if (services is null) throw new ArgumentNullException(nameof(services));
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Key is required.", nameof(key));

        services.Add(new ServiceDescriptor(typeof(TCommand), typeof(TCommand), lifetime));

        services.AddSingleton(new CallbackCommandDescriptor(
            key,
            static (query, args, ctx) =>
            {
                var handler = ctx.Services.GetRequiredService<TCommand>();
                return new ValueTask(handler.HandleAsync(query, args, ctx));
            }));

        return services;
    }

    /// <summary>
    /// Registers all TelegramBotKit commands.
    /// If generated registrations are available (TelegramBotKit.Generators installed), uses them.
    /// Otherwise falls back to reflection-based discovery.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="fallbackToReflection">
    /// If false, throws when no generated registrations are present.
    /// Use false for NativeAOT/trim-safe builds.
    /// </param>
    public static IServiceCollection AddCommands(this IServiceCollection services, bool fallbackToReflection = true)
    {
        ArgumentNullException.ThrowIfNull(services);

        // 1) Prefer generated registrations (no reflection)
        if (TelegramBotKitGeneratedCommandsHook.TryRun(services))
            return services;

        // 2) Fallback to reflection discovery
        if (!fallbackToReflection)
            throw new InvalidOperationException(
                "No generated command registrations found. Install TelegramBotKit.Generators or enable reflection fallback.");

        return AddCommandsByReflection(services);
    }

    private static IServiceCollection AddCommandsByReflection(IServiceCollection services)
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a is not null && !a.IsDynamic)
            .Where(ReferencesTelegramBotKit)
            .ToArray();

        foreach (var t in GetTypesWithCommandAttributes(assemblies))
            AddCommandByType(services, t);

        return services;
    }

    private static bool ReferencesTelegramBotKit(Assembly asm)
    {
        var name = asm.GetName().Name;
        if (string.Equals(name, "TelegramBotKit", StringComparison.OrdinalIgnoreCase))
            return true;

        try
        {
            return asm.GetReferencedAssemblies().Any(r => string.Equals(r.Name, "TelegramBotKit", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    private static IEnumerable<Type> GetTypesWithCommandAttributes(params Assembly[] assemblies)
    {
        foreach (var asm in assemblies)
        {
            Type[] types;
            try
            {
                types = asm.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t is not null).Cast<Type>().ToArray();
            }

            foreach (var t in types)
            {
                if (t is null) continue;
                if (t.IsAbstract || t.IsInterface) continue;
                if (!typeof(ICommand).IsAssignableFrom(t)) continue;

                var hasRoleAttr = t.GetCustomAttributes(typeof(MessageCommandAttribute), inherit: false).Length > 0
                    || t.GetCustomAttributes(typeof(TextCommandAttribute), inherit: false).Length > 0
                    || t.GetCustomAttributes(typeof(CallbackCommandAttribute), inherit: false).Length > 0;

                if (hasRoleAttr)
                    yield return t;
            }
        }
    }

    private static void AddCommandByType(IServiceCollection services, Type commandType, ServiceLifetime lifetime = ServiceLifetime.Transient)
    {
        services.Add(new ServiceDescriptor(commandType, commandType, lifetime));

        var msgAttr = commandType.GetCustomAttribute<MessageCommandAttribute>();
        if (msgAttr is not null && typeof(IMessageCommand).IsAssignableFrom(commandType))
        {
            services.AddSingleton(new MessageCommandDescriptor(
                msgAttr.Command,
                (message, ctx) =>
                {
                    var handler = (IMessageCommand)ctx.Services.GetRequiredService(commandType);
                    return new ValueTask(handler.HandleAsync(message, ctx));
                }));
        }

        var textAttr = commandType.GetCustomAttribute<TextCommandAttribute>();
        if (textAttr is not null && typeof(ITextCommand).IsAssignableFrom(commandType))
        {
            services.AddSingleton(new TextCommandDescriptor(
                textAttr.Triggers,
                textAttr.IgnoreCase,
                (message, ctx) =>
                {
                    var handler = (ITextCommand)ctx.Services.GetRequiredService(commandType);
                    return new ValueTask(handler.HandleAsync(message, ctx));
                }));
        }

        var cbAttr = commandType.GetCustomAttribute<CallbackCommandAttribute>();
        if (cbAttr is not null && typeof(ICallbackCommand).IsAssignableFrom(commandType))
        {
            services.AddSingleton(new CallbackCommandDescriptor(
                cbAttr.Key,
                (query, args, ctx) =>
                {
                    var handler = (ICallbackCommand)ctx.Services.GetRequiredService(commandType);
                    return new ValueTask(handler.HandleAsync(query, args, ctx));
                }));
        }
    }
}
