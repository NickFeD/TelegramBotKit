using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using System.Net.Http;
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
/// Registers Core services, generated or explicit commands, and optional queued messaging.
/// </summary>
/// <remarks>
/// Core registration does not start polling or map a webhook endpoint; register a delivery adapter separately.
/// Repeated AddTelegramBotKit calls reuse the builder and append options configuration. The first
/// registration supplies the HTTP-client factory unless a Telegram client was already registered.
/// </remarks>
public static partial class TelegramBotKitServiceCollectionExtensions
{
    /// <summary>
    /// Registers Core dispatch, command routing, message sending and in-memory response waits.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configure">Options configuration delegate.</param>
    /// <returns>A <see cref="TelegramBotKitBuilder"/> instance for further configuration.</returns>
    public static TelegramBotKitBuilder AddTelegramBotKit(
        this IServiceCollection services,
        Action<TelegramBotKitOptions> configure)
    {
        return AddTelegramBotKit(services, configure, static _ => null);
    }

    /// <summary>
    /// Registers Core services using the supplied <see cref="HttpClient"/> for Telegram requests.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configure">Options configuration delegate.</param>
    /// <param name="httpClient">
    /// Custom <see cref="HttpClient"/> used to construct <see cref="ITelegramBotClient"/>.
    /// </param>
    /// <returns>A <see cref="TelegramBotKitBuilder"/> instance for further configuration.</returns>
    public static TelegramBotKitBuilder AddTelegramBotKit(
        this IServiceCollection services,
        Action<TelegramBotKitOptions> configure,
        HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        return AddTelegramBotKit(services, configure, _ => httpClient);
    }

    /// <summary>
    /// Registers Core services using a dependency-injection factory for the Telegram HTTP client.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configure">Options configuration delegate.</param>
    /// <param name="httpClientFactory">
    /// Factory invoked with the root service provider when the singleton Telegram client is created.
    /// If the factory returns <see langword="null"/>, the default <see cref="TelegramBotClient"/> constructor is used.
    /// </param>
    /// <returns>A <see cref="TelegramBotKitBuilder"/> instance for further configuration.</returns>
    public static TelegramBotKitBuilder AddTelegramBotKit(
        this IServiceCollection services,
        Action<TelegramBotKitOptions> configure,
        Func<IServiceProvider, HttpClient?> httpClientFactory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        ArgumentNullException.ThrowIfNull(httpClientFactory);

        var registration = services.FirstOrDefault(static descriptor =>
            descriptor.ServiceType == typeof(TelegramBotKitRegistration))?.ImplementationInstance
            as TelegramBotKitRegistration;
        if (registration is not null)
        {
            registration.Builder.Registry.EnsureMutable();
            services.Configure(configure);
            return registration.Builder;
        }

        services.AddOptions<TelegramBotKitOptions>()
            .Configure(configure)
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<TelegramBotKitOptions>, TelegramBotKitOptionsValidator>();

        services.TryAddSingleton<ITelegramBotClient>(sp =>
        {
            var opt = sp.GetRequiredService<IOptions<TelegramBotKitOptions>>().Value;
            var clientOptions = new TelegramBotClientOptions(opt.Token);
            var httpClient = httpClientFactory(sp);

            return httpClient is null
                ? new TelegramBotClient(clientOptions)
                : new TelegramBotClient(clientOptions, httpClient);
        });

        services.TryAddSingleton<MessageSender>();
        services.TryAddSingleton<IMessageSender>(sp => sp.GetRequiredService<MessageSender>());

        services.AddSingleton<WaitForUserResponse>();

        services.AddScoped<CommandRouter>();
        services.TryAddSingleton<CommandRegistry>();


        services.TryAddSingleton<IDefaultUpdateHandler, NoopDefaultUpdateHandler>();
        services.TryAddSingleton<IDefaultMessageHandler, NoopDefaultMessageHandler>();
        services.TryAddSingleton<IDefaultCallbackHandler, NoopDefaultCallbackHandler>();

        services.TryAddScoped<MessageUpdateHandler>();
        services.TryAddScoped<CallbackQueryUpdateHandler>();
        var builder = new TelegramBotKitBuilder(services);

        services.AddSingleton(sp => new MiddlewarePipeline(builder.MiddlewareFactories));

        services.AddSingleton(sp =>
        {
            var reg = builder.Registry;
            // Defaults only fill routes that the application has not explicitly configured.
            if (!reg.Contains(UpdateType.Message))
                reg.GetOrAdd(UpdateRoutes.Message).SetTerminal<MessageUpdateHandler>();
            if (!reg.Contains(UpdateType.CallbackQuery))
                reg.GetOrAdd(UpdateRoutes.CallbackQuery).SetTerminal<CallbackQueryUpdateHandler>();

            reg.Freeze();
            return reg;
        });

        services.TryAddSingleton<IUpdateDispatcher, UpdateRouter>();
        services.AddSingleton(new TelegramBotKitRegistration(builder));

        return builder;
    }

    // Stored on the service collection, not in a process-wide cache or a second registry.
    private sealed record TelegramBotKitRegistration(TelegramBotKitBuilder Builder);

    /// <summary>
    /// Replaces <see cref="IMessageSender"/> with an in-memory queue that applies delivery limits and retries.
    /// </summary>
    /// <param name="services">The service collection receiving the registrations.</param>
    /// <param name="configure">An optional delegate that configures queue capacity, throttling and retries.</param>
    /// <returns>The same service collection for further configuration.</returns>
    public static IServiceCollection AddTelegramBotKitQueuedMessageSender(
        this IServiceCollection services,
        Action<QueuedMessageSenderOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var opt = services.AddOptions<QueuedMessageSenderOptions>();
        if (configure is not null)
            opt.Configure(configure);

        services.TryAddSingleton<MessageSender>();
        services.TryAddSingleton<QueuedMessageSender>();

        services.Replace(ServiceDescriptor.Singleton<IMessageSender>(sp => sp.GetRequiredService<QueuedMessageSender>()));

        return services;
    }

    /// <summary>
    /// Registers a slash-message command, such as <c>/start</c>, without assembly scanning.
    /// The command instance is resolved from the per-update scope (<see cref="BotContext.Services"/>).
    /// </summary>
    /// <typeparam name="TCommand">Concrete command type.</typeparam>
    /// <param name="services">Service collection.</param>
    /// <param name="command">Slash command string ("/start").</param>
    /// <param name="lifetime">DI lifetime for the command type. Defaults to <see cref="ServiceLifetime.Transient"/>.</param>
    /// <returns>The same service collection for further configuration.</returns>
    public static IServiceCollection AddMessageCommand<TCommand>(
        this IServiceCollection services,
        string command,
        ServiceLifetime lifetime = ServiceLifetime.Transient)
        where TCommand : class, IMessageCommand
    {
        ArgumentNullException.ThrowIfNull(services);

        if (string.IsNullOrWhiteSpace(command))
            throw new ArgumentException("Command is required.", nameof(command));

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
    /// Registers an exact text-trigger command without assembly scanning.
    /// The command instance is resolved from the per-update scope (<see cref="BotContext.Services"/>).
    /// </summary>
    /// <typeparam name="TCommand">Concrete command type.</typeparam>
    /// <param name="services">Service collection.</param>
    /// <param name="triggers">Text triggers (exact match).</param>
    /// <param name="ignoreCase">Whether triggers should match ignoring case.</param>
    /// <param name="lifetime">DI lifetime for the command type. Defaults to <see cref="ServiceLifetime.Transient"/>.</param>
    /// <returns>The same service collection for further configuration.</returns>
    public static IServiceCollection AddTextCommand<TCommand>(
        this IServiceCollection services,
        IReadOnlyList<string> triggers,
        bool ignoreCase = true,
        ServiceLifetime lifetime = ServiceLifetime.Transient)
        where TCommand : class, ITextCommand
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(triggers);

        var normalizedTriggers = triggers
            .Where(static t => !string.IsNullOrWhiteSpace(t))
            .Select(static t => t.Trim())
            .Distinct(ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
            .ToArray();

        if (normalizedTriggers.Length == 0)
            throw new ArgumentException("At least one non-empty trigger is required.", nameof(triggers));

        services.Add(new ServiceDescriptor(typeof(TCommand), typeof(TCommand), lifetime));

        services.AddSingleton(new TextCommandDescriptor(
            normalizedTriggers,
            ignoreCase,
            static (message, ctx) =>
            {
                var handler = ctx.Services.GetRequiredService<TCommand>();
                return new ValueTask(handler.HandleAsync(message, ctx));
            }));

        return services;
    }

    /// <summary>
    /// Registers a callback command without assembly scanning.
    /// The command instance is resolved from the per-update scope (<see cref="BotContext.Services"/>).
    /// </summary>
    /// <typeparam name="TCommand">Concrete command type.</typeparam>
    /// <param name="services">Service collection.</param>
    /// <param name="key">Callback key.</param>
    /// <param name="lifetime">DI lifetime for the command type. Defaults to <see cref="ServiceLifetime.Transient"/>.</param>
    /// <returns>The same service collection for further configuration.</returns>
    public static IServiceCollection AddCallbackCommand<TCommand>(
        this IServiceCollection services,
        string key,
        ServiceLifetime lifetime = ServiceLifetime.Transient)
        where TCommand : class, ICallbackCommand
    {
        ArgumentNullException.ThrowIfNull(services);

        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key is required.", nameof(key));

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

    /// <summary>Registers attributed commands discovered by the bundled source generator.</summary>
    /// <param name="services">Service collection receiving generated registrations.</param>
    /// <param name="fallbackToReflection">Legacy compatibility flag. When false, missing generated registrations cause an error. Runtime reflection discovery is never performed.</param>
    /// <returns>The same collection for continued configuration.</returns>
    /// <remarks>Explicit generic and fluent registration remains available without discovery. Empty applications can keep the default call.</remarks>
    /// <exception cref="InvalidOperationException">No generated registrar is available and <paramref name="fallbackToReflection"/> is false.</exception>
    public static IServiceCollection AddCommands(this IServiceCollection services, bool fallbackToReflection = true)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (!TelegramBotKitGeneratedCommandsHook.TryRun(services) && !fallbackToReflection)
            throw new InvalidOperationException("No generated command registrations found. Enable the generator bundled with TelegramBotKit, or register commands explicitly.");
        return services;
    }
}
