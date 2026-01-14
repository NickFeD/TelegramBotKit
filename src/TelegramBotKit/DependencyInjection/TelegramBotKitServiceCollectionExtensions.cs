using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using System.Linq.Expressions;
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
public static class TelegramBotKitServiceCollectionExtensions
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

        services.AddSingleton(sp => new MiddlewarePipeline(sp, builder.MiddlewareTypes));

        services.AddSingleton<UpdateHandlerRegistry>(sp =>
        {
            var reg = new UpdateHandlerRegistry();

            MapUpdatePayloadsByConvention(reg);

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

    private static void MapUpdatePayloadsByConvention(UpdateHandlerRegistry reg)
    {
        var mapGeneric = typeof(UpdateHandlerRegistry)
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(m => m.Name == nameof(UpdateHandlerRegistry.Map))
            .Single(m => m.IsGenericMethodDefinition && m.GetParameters().Length == 2);

        var updateTypeValues = Enum.GetValues<UpdateType>();

        foreach (var updateType in updateTypeValues)
        {
            var name = updateType.ToString();

            var prop = typeof(Update).GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            if (prop is null) continue;

            var payloadType = prop.PropertyType;
            if (!payloadType.IsClass) continue;

            var u = Expression.Parameter(typeof(Update), "u");
            var body = Expression.Property(u, prop);
            var funcType = typeof(Func<,>).MakeGenericType(typeof(Update), payloadType);
            var extractor = Expression.Lambda(funcType, body, u).Compile();

            var closedMap = mapGeneric.MakeGenericMethod(payloadType);
            closedMap.Invoke(reg, new object[] { updateType, extractor });
        }
    }


    /// <summary>
    /// Adds the command.
    /// </summary>
    public static IServiceCollection AddCommand<TCommand>(this IServiceCollection services, ServiceLifetime lifetime = ServiceLifetime.Transient)
        where TCommand : class, ICommand
        => services.AddCommand(typeof(TCommand), lifetime);

    /// <summary>
    /// Adds the command.
    /// </summary>
    public static IServiceCollection AddCommand(this IServiceCollection services, Type commandType, ServiceLifetime lifetime = ServiceLifetime.Transient)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));
        if (commandType is null) throw new ArgumentNullException(nameof(commandType));

        if (!typeof(ICommand).IsAssignableFrom(commandType))
            throw new ArgumentException($"{commandType.Name} does not implement ICommand.", nameof(commandType));

        if (commandType.IsAbstract || commandType.IsInterface)
            throw new ArgumentException($"{commandType.Name} must be a concrete class.", nameof(commandType));

        services.Add(new ServiceDescriptor(commandType, commandType, lifetime));

        var hasAnyRoleAttr = false;

        var msgAttr = commandType.GetCustomAttribute<MessageCommandAttribute>();
        if (msgAttr is not null)
        {
            hasAnyRoleAttr = true;
            if (!typeof(IMessageCommand).IsAssignableFrom(commandType))
                throw new ArgumentException($"{commandType.Name} has [MessageCommand] but does not implement IMessageCommand.", nameof(commandType));

            services.AddSingleton(new MessageCommandDescriptor(msgAttr.Command, commandType));
        }

        var textAttr = commandType.GetCustomAttribute<TextCommandAttribute>();
        if (textAttr is not null)
        {
            hasAnyRoleAttr = true;
            if (!typeof(ITextCommand).IsAssignableFrom(commandType))
                throw new ArgumentException($"{commandType.Name} has [TextCommand] but does not implement ITextCommand.", nameof(commandType));

            services.AddSingleton(new TextCommandDescriptor(textAttr.Triggers, textAttr.IgnoreCase, commandType));
        }

        var cbAttr = commandType.GetCustomAttribute<CallbackCommandAttribute>();
        if (cbAttr is not null)
        {
            hasAnyRoleAttr = true;
            if (!typeof(ICallbackCommand).IsAssignableFrom(commandType))
                throw new ArgumentException($"{commandType.Name} has [CallbackCommand] but does not implement ICallbackCommand.", nameof(commandType));

            services.AddSingleton(new CallbackCommandDescriptor(cbAttr.Key, commandType));
        }

        if (!hasAnyRoleAttr)
        {
            var hasInterfaces = typeof(IMessageCommand).IsAssignableFrom(commandType)
                || typeof(ITextCommand).IsAssignableFrom(commandType)
                || typeof(ICallbackCommand).IsAssignableFrom(commandType);

            if (hasInterfaces)
                throw new ArgumentException($"{commandType.Name} implements command interfaces but has no command metadata attributes. Add [MessageCommand], [TextCommand] or [CallbackCommand].", nameof(commandType));
        }

        return services;
    }

    /// <summary>
    /// Adds the commands from assemblies.
    /// </summary>
    public static IServiceCollection AddCommandsFromAssemblies(this IServiceCollection services, params Assembly[] assemblies)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));
        if (assemblies is null || assemblies.Length == 0)
            throw new ArgumentException("At least one assembly is required.", nameof(assemblies));

        foreach (var t in GetTypesWithCommandAttributes(assemblies))
            services.AddCommand(t);

        return services;
    }

    private static IEnumerable<Type> GetTypesWithCommandAttributes(params Assembly[] assemblies)
    {
        foreach (var asm in assemblies.Where(a => a is not null && !a.IsDynamic))
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
}
