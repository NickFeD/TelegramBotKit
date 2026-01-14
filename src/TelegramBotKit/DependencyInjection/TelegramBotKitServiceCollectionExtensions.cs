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

// ------------------- Extensions -------------------

public static class TelegramBotKitServiceCollectionExtensions
{
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

        // Telegram client (Telegram.Bot v22.*)
        services.AddSingleton<ITelegramBotClient>(sp =>
        {
            var opt = sp.GetRequiredService<IOptions<TelegramBotKitOptions>>().Value;
            var clientOptions = new TelegramBotClientOptions(opt.Token);
            return new TelegramBotClient(clientOptions);
        });

        // Sender (по умолчанию без очереди)
        services.TryAddSingleton<MessageSender>();
        services.TryAddSingleton<IMessageSender>(sp => sp.GetRequiredService<MessageSender>());

        // Conversations
        services.AddSingleton<WaitForUserResponse>();

        // Routing
        services.AddScoped<CommandRouter>();
        services.TryAddSingleton<CommandRegistry>();

        // Update payload handlers (scope-per-update)
        services.AddScoped<IUpdatePayloadHandler<Message>, MessageUpdateHandler>();
        services.AddScoped<IUpdatePayloadHandler<CallbackQuery>, CallbackQueryUpdateHandler>();

        // Default handlers (noop by default)
        services.TryAddSingleton<IDefaultUpdateHandler, NoopDefaultUpdateHandler>();
        services.TryAddSingleton<IDefaultMessageHandler, NoopDefaultMessageHandler>();
        services.TryAddSingleton<IDefaultCallbackHandler, NoopDefaultCallbackHandler>();

        var builder = new TelegramBotKitBuilder(services);

        // Pipeline
        services.AddSingleton(sp => new MiddlewarePipeline(sp, builder.MiddlewareTypes));

        // Registry + default mappings
        services.AddSingleton<UpdateHandlerRegistry>(sp =>
        {
            var reg = new UpdateHandlerRegistry();

            // По умолчанию маппим *все* UpdateType по соглашению:
            // если в Telegram.Bot.Types.Update есть property с таким же именем, как enum UpdateType,
            // то делаем Map(UpdateType.X, u => u.X).
            // Это даёт «из коробки» поддержку новых типов без маркерных интерфейсов.
            MapUpdatePayloadsByConvention(reg);

            // Маппинги, добавленные через builder.Map(...)
            foreach (var add in builder.RegistryActions)
                add(reg);

            reg.Freeze();
            return reg;
        });

        services.AddSingleton<IUpdateDispatcher, UpdateRouter>();

        // IMPORTANT: return the same builder instance.
        // Otherwise middleware/commands added by the caller after AddTelegramBotKit(...)
        // would be added to a different builder and never make it into the pipeline.
        return builder;
    }

    // ------------------- Update handler registration -------------------

    /// <summary>
    /// Удобная регистрация обработчика payload-типа.
    ///
    /// ВАЖНО: регистрируй именно как IUpdatePayloadHandler&lt;TPayload&gt;,
    /// иначе стандартный DI не сможет найти обработчик по generic-интерфейсу.
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


// ------------------- Message sender decorators -------------------

/// <summary>
/// Включить очередной sender с троттлингом и ретраями (защита от 429/5xx).
/// По умолчанию AddTelegramBotKit регистрирует простой MessageSender без очереди.
/// </summary>
public static IServiceCollection AddTelegramBotKitQueuedMessageSender(
    this IServiceCollection services,
    Action<QueuedMessageSenderOptions>? configure = null)
{
    if (services is null) throw new ArgumentNullException(nameof(services));

    var opt = services.AddOptions<QueuedMessageSenderOptions>();
    if (configure is not null)
        opt.Configure(configure);

    // Базовый sender (внутренний)
    services.TryAddSingleton<MessageSender>();

    // Декоратор
    services.TryAddSingleton<QueuedMessageSender>();

    // IMessageSender -> QueuedMessageSender
    services.Replace(ServiceDescriptor.Singleton<IMessageSender>(sp => sp.GetRequiredService<QueuedMessageSender>()));

    return services;
}

    private static void MapUpdatePayloadsByConvention(UpdateHandlerRegistry reg)
    {
        // Ищем UpdateHandlerRegistry.Map<TPayload>(UpdateType, Func<Update, TPayload?>)
        var mapGeneric = typeof(UpdateHandlerRegistry)
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(m => m.Name == nameof(UpdateHandlerRegistry.Map))
            .Single(m => m.IsGenericMethodDefinition && m.GetParameters().Length == 2);

        var updateTypeValues = Enum.GetValues<UpdateType>();

        foreach (var updateType in updateTypeValues)
        {
            var name = updateType.ToString();

            // Ищем property на Update с таким же именем (Message, CallbackQuery, InlineQuery, ...)
            var prop = typeof(Update).GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            if (prop is null) continue;

            var payloadType = prop.PropertyType;
            if (!payloadType.IsClass) continue; // Map работает только для reference types

            // Компилируем extractor: (Update u) => u.<Prop>
            var u = Expression.Parameter(typeof(Update), "u");
            var body = Expression.Property(u, prop);
            var funcType = typeof(Func<,>).MakeGenericType(typeof(Update), payloadType);
            var extractor = Expression.Lambda(funcType, body, u).Compile();

            // Вызываем Map<TPayload>(updateType, extractor) через reflection
            var closedMap = mapGeneric.MakeGenericMethod(payloadType);
            closedMap.Invoke(reg, new object[] { updateType, extractor });
        }
    }

    // ------------------- Command registration -------------------

    public static IServiceCollection AddCommand<TCommand>(this IServiceCollection services, ServiceLifetime lifetime = ServiceLifetime.Transient)
        where TCommand : class, ICommand
        => services.AddCommand(typeof(TCommand), lifetime);

    public static IServiceCollection AddCommand(this IServiceCollection services, Type commandType, ServiceLifetime lifetime = ServiceLifetime.Transient)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));
        if (commandType is null) throw new ArgumentNullException(nameof(commandType));

        if (!typeof(ICommand).IsAssignableFrom(commandType))
            throw new ArgumentException($"{commandType.Name} does not implement ICommand.", nameof(commandType));

        if (commandType.IsAbstract || commandType.IsInterface)
            throw new ArgumentException($"{commandType.Name} must be a concrete class.", nameof(commandType));

        // Регистрируем конкретный тип (инстанс будет создаваться только когда команда реально выбрана)
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

        // Если класс реализует command-интерфейсы, но атрибутов нет — мы не можем построить индекс.
        // Это сделано специально: команда больше НЕ создаётся на каждый update.
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
