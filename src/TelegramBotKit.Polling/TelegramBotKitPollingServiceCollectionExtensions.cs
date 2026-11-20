using Microsoft.Extensions.DependencyInjection;
using TelegramBotKit.Hosting;
using TelegramBotKit.Options;

namespace TelegramBotKit.Polling;

/// <summary>Registers Telegram long polling backed by the shared update receiver.</summary>
public static class TelegramBotKitPollingServiceCollectionExtensions
{
    /// <summary>Registers one host-managed GetUpdates loop and shared update execution.</summary>
    /// <param name="services">The application's services, including AddTelegramBotKit registrations.</param>
    /// <returns>The service collection for further configuration.</returns>
    /// <remarks>
    /// Repeated calls are idempotent. Cannot be combined with webhook delivery in the same container.
    /// The next polling offset advances after receiver admission, not after handler completion.
    /// Default admission is in memory and does not survive a process restart.
    /// </remarks>
    public static IServiceCollection AddTelegramBotKitPolling(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddTelegramBotKitUpdateProcessing(UpdateDeliveryMode.Polling);
        services.AddHostedService<PollingHostedService>();
        return services;
    }
}
