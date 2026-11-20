using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramBotKit.Messaging;

namespace TelegramBotKit;

/// <summary>
/// Carries an incoming update, Telegram clients and scoped services through its processing pipeline.
/// </summary>
public sealed class BotContext
{
    private IDictionary<string, object?>? _items;

    /// <summary>
    /// Creates a context using the supplied update, clients and service provider.
    /// </summary>
    /// <param name="update">The incoming Telegram update.</param>
    /// <param name="botClient">The Telegram client available to the handler.</param>
    /// <param name="sender">The message sender available to the handler.</param>
    /// <param name="services">The service provider for the current update scope.</param>
    /// <param name="cancellationToken">The token exposed to processing code for this update.</param>
    public BotContext(
        Update update,
        ITelegramBotClient botClient,
        IMessageSender sender,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        Update = update ?? throw new ArgumentNullException(nameof(update));
        BotClient = botClient ?? throw new ArgumentNullException(nameof(botClient));
        Sender = sender ?? throw new ArgumentNullException(nameof(sender));
        Services = services ?? throw new ArgumentNullException(nameof(services));
        CancellationToken = cancellationToken;
    }

    /// <summary>
    /// Gets the incoming update.
    /// </summary>
    public Update Update { get; }


    /// <summary>
    /// Gets the message sender.
    /// </summary>
    public IMessageSender Sender { get; }

    /// <summary>
    /// Gets the Telegram bot client.
    /// </summary>
    public ITelegramBotClient BotClient { get; }

    /// <summary>
    /// Gets the service provider for this update's dependency-injection scope.
    /// </summary>
    public IServiceProvider Services { get; }

    /// <summary>
    /// Gets the token that handlers should observe when processing this update.
    /// </summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// Gets the lazily allocated, case-sensitive item collection shared by this update's middleware and handlers.
    /// </summary>
    public IDictionary<string, object?> Items
        => _items ??= new Dictionary<string, object?>(StringComparer.Ordinal);

    /// <summary>
    /// Resolves a required service from the current update scope.
    /// </summary>
    /// <typeparam name="T">The required service type.</typeparam>
    /// <returns>The required service resolved from this update's service provider.</returns>
    public T GetRequiredService<T>() where T : notnull => Services.GetRequiredService<T>();
}
