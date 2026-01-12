using Microsoft.Extensions.DependencyInjection;

namespace TelegramBotKit.Hosting;

public static class TelegramBotKitHostingServiceCollectionExtensions
{
    public static IServiceCollection AddTelegramBotKitPolling(this IServiceCollection services)
    {
        services.AddHostedService<PollingHostedService>();
        return services;
    }
}
