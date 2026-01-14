using Microsoft.Extensions.DependencyInjection;

namespace TelegramBotKit.Hosting;

public static class TelegramBotKitHostingServiceCollectionExtensions
{
    public static IServiceCollection AddTelegramBotKitPolling(this IServiceCollection services)
    {
        services.AddSingleton<UpdateActorScheduler>();
        services.AddHostedService<PollingHostedService>();
        return services;
    }
}
