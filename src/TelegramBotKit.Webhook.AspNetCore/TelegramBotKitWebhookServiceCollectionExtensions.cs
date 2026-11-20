using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using TelegramBotKit.Hosting;
using TelegramBotKit.Options;

namespace TelegramBotKit.Webhook.AspNetCore;

/// <summary>Registers optional ASP.NET Core webhook delivery.</summary>
public static class TelegramBotKitWebhookServiceCollectionExtensions
{
    /// <summary>Registers webhook options and shared update execution, without contacting Telegram.</summary>
    /// <param name="services">Application services containing AddTelegramBotKit registrations.</param>
    /// <param name="configure">Configures the endpoint path, required secret and optional management URL.</param>
    /// <returns>The service collection.</returns>
    /// <remarks>Register webhook or polling for one runtime. Map the endpoint separately.</remarks>
    public static IServiceCollection AddTelegramBotKitWebhook(
        this IServiceCollection services, Action<WebhookOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        services.AddTelegramBotKitUpdateProcessing(UpdateDeliveryMode.Webhook);
        services.AddOptions<WebhookOptions>().Configure(configure).ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<WebhookOptions>, WebhookOptionsValidator>());
        return services;
    }
}
