using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using TelegramBotKit.Options;

namespace TelegramBotKit.Webhook.AspNetCore;

/// <summary>Provides explicit Telegram webhook management, independent of application startup and shutdown.</summary>
public static class TelegramBotKitWebhookManagementExtensions
{
    /// <summary>Registers the configured public HTTPS URL with Telegram.</summary>
    /// <param name="services">Application services configured with Core and webhook delivery.</param>
    /// <param name="ct">Cancels the Telegram API request.</param>
    /// <returns>The registration operation.</returns>
    /// <remarks>Call deliberately during deployment. Never called automatically by the hosted services.</remarks>
    public static Task SetTelegramBotKitWebhookAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        var options = services.GetRequiredService<IOptions<WebhookOptions>>().Value;
        if (string.IsNullOrWhiteSpace(options.Url))
            throw new InvalidOperationException("Webhook Url is required for explicit Telegram registration.");
        return services.GetRequiredService<ITelegramBotClient>().SetWebhook(
            url: options.Url,
            maxConnections: options.MaxConnections,
            allowedUpdates: options.AllowedUpdates,
            dropPendingUpdates: options.DropPendingUpdates,
            secretToken: options.SecretToken,
            cancellationToken: ct);
    }

    /// <summary>Explicitly removes the bot's webhook registration from Telegram.</summary>
    /// <param name="services">Application services containing ITelegramBotClient.</param>
    /// <param name="dropPendingUpdates">Whether Telegram should discard pending updates. Defaults to false.</param>
    /// <param name="ct">Cancels the Telegram API request.</param>
    /// <returns>The removal operation.</returns>
    /// <remarks>Never call automatically when an arbitrary replica stops.</remarks>
    public static Task DeleteTelegramBotKitWebhookAsync(
        this IServiceProvider services, bool dropPendingUpdates = false, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.GetRequiredService<ITelegramBotClient>()
            .DeleteWebhook(dropPendingUpdates: dropPendingUpdates, cancellationToken: ct);
    }
}
