using Microsoft.Extensions.DependencyInjection;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using TelegramBotKit.Dispatching;
using TelegramBotKit.Options;

namespace TelegramBotKit.Hosting;

/// <summary>
/// Registers shared update execution independently of its delivery transport.
/// </summary>
public static class TelegramBotKitHostingServiceCollectionExtensions
{
    /// <summary>Registers shared in-memory update execution and its host-managed lifetime.</summary>
    /// <param name="services">The application's services.</param>
    /// <param name="deliveryMode">
    /// Optional transport selected by an adapter. Polling and webhook cannot be selected together
    /// in one container. Omit when registering execution without choosing a transport.
    /// </param>
    /// <param name="configure">Optional shared execution settings, independent of the transport.</param>
    /// <returns>The service collection for further configuration.</returns>
    /// <remarks>An existing IUpdateReceiver registration is preserved.</remarks>
    public static IServiceCollection AddTelegramBotKitUpdateProcessing(
        this IServiceCollection services, UpdateDeliveryMode? deliveryMode = null,
        Action<UpdateProcessingOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (deliveryMode is { } mode)
        {
            if (mode is not (UpdateDeliveryMode.Polling or UpdateDeliveryMode.Webhook))
                throw new ArgumentOutOfRangeException(nameof(deliveryMode));
            var existing = services.FirstOrDefault(d => d.ServiceType == typeof(TransportRegistration))
                ?.ImplementationInstance as TransportRegistration;
            if (existing is not null && existing.Mode != mode)
                throw new InvalidOperationException("Polling and webhook cannot be enabled together for one TelegramBotKit runtime.");
            if (existing is null) services.AddSingleton(new TransportRegistration(mode));
        }
        services.AddOptions<UpdateProcessingOptions>();
        if (configure is not null) services.Configure(configure);
        services.TryAddSingleton(sp => new UpdateActorScheduler(
            sp.GetRequiredService<IUpdateDispatcher>(),
            sp.GetRequiredService<IOptions<TelegramBotKitOptions>>(),
            sp.GetRequiredService<ILogger<UpdateActorScheduler>>(), TimeProvider.System,
            sp.GetRequiredService<IOptions<UpdateProcessingOptions>>().Value.MaxDegreeOfParallelism));
        services.TryAddSingleton<IUpdateReceiver>(sp => sp.GetRequiredService<UpdateActorScheduler>());
        services.AddHostedService(sp => sp.GetRequiredService<UpdateActorScheduler>());
        return services;
    }

    private sealed record TransportRegistration(UpdateDeliveryMode Mode);
}
