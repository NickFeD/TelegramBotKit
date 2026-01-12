using Microsoft.Extensions.DependencyInjection;

namespace TelegramBotKit.Hosting;

/// <summary>
/// Подключает polling-hosting для TelegramBotKit.
/// </summary>
public static class TelegramBotKitHostingServiceCollectionExtensions
{
    public static IServiceCollection AddTelegramBotKitPolling(this IServiceCollection services)
    {
        services.AddHostedService<PollingHostedService>();
        return services;
    }
}
