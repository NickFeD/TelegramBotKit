using Microsoft.Extensions.DependencyInjection;

namespace TelegramBotKit.Hosting;

/// <summary>
/// Provides a telegram bot kit hosting service collection extensions.
/// </summary>
public static class TelegramBotKitHostingServiceCollectionExtensions
{
    /// <summary>
    /// Adds the telegram bot kit polling.
    /// </summary>
    public static IServiceCollection AddTelegramBotKitPolling(this IServiceCollection services)
    {
        services.AddSingleton<UpdateActorScheduler>();
        services.AddHostedService<PollingHostedService>();
        return services;
    }
}
