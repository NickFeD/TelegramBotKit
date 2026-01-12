using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Reflection;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramBotKit.Commands;
using TelegramBotKit.Conversations;
using TelegramBotKit.Dispatching;
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

        // Sender
        services.AddSingleton<IMessageSender, MessageSender>();

        // Conversations
        services.AddSingleton<WaitForUserResponse>();

        // Routing
        services.AddScoped<CommandRouter>();

        // Update payload handlers (scope-per-update)
        services.AddScoped<IUpdatePayloadHandler<Message>, MessageUpdateHandler>();
        services.AddScoped<IUpdatePayloadHandler<CallbackQuery>, CallbackQueryUpdateHandler>();


        // Pipeline
        services.AddSingleton<MiddlewarePipeline>(sp =>
        {
            var pipeline = new MiddlewarePipeline();
            foreach (var cfg in sp.GetServices<IMiddlewareConfigurator>())
                cfg.Configure(pipeline);

            pipeline.Freeze();
            return pipeline;
        });

        // Registry + default mappings
        services.AddSingleton<UpdateHandlerRegistry>(sp =>
        {
            var reg = new UpdateHandlerRegistry();

            reg.Map<Message>(UpdateType.Message, u => u.Message);
            reg.Map<CallbackQuery>(UpdateType.CallbackQuery, u => u.CallbackQuery);

            foreach (var cfg in sp.GetServices<IRegistryConfigurator>())
                cfg.Configure(reg);

            reg.Freeze();
            return reg;
        });

        services.AddSingleton<UpdateRouter>();

        return new TelegramBotKitBuilder(services);
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

        // Регистрируем конкретный тип
        services.Add(new ServiceDescriptor(commandType, commandType, lifetime));

        // Регистрируем “роли” (если реализованы)
        var roles = new[] { typeof(IMessageCommand), typeof(ICallbackCommand), typeof(ITextCommand) };

        foreach (var role in roles)
        {
            if (role.IsAssignableFrom(commandType))
                services.Add(new ServiceDescriptor(role, sp => sp.GetRequiredService(commandType), lifetime));
        }

        return services;
    }

    public static IServiceCollection AddCommandsFromAssemblies(this IServiceCollection services, params Assembly[] assemblies)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));
        if (assemblies is null || assemblies.Length == 0)
            throw new ArgumentException("At least one assembly is required.", nameof(assemblies));

        foreach (var t in GetTypesWithAttribute<CommandAttribute>(assemblies))
            services.AddCommand(t);

        return services;
    }

    private static IEnumerable<Type> GetTypesWithAttribute<TAttribute>(params Assembly[] assemblies)
        where TAttribute : Attribute
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

                // Атрибут = маячок
                if (t.GetCustomAttributes(typeof(TAttribute), inherit: false).Length > 0)
                    yield return t;
            }
        }
    }
}
