using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramBotKit.Messaging;

namespace TelegramBotKit;

/// <summary>
/// Provides a bot context.
/// </summary>
public sealed class BotContext
{
    /// <summary>
    /// Initializes a new instance of BotContext.
    /// </summary>
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
        Items = new Dictionary<string, object?>(StringComparer.Ordinal);
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
    /// Gets the service provider.
    /// </summary>
    public IServiceProvider Services { get; }

    /// <summary>
    /// Gets the cancellation token.
    /// </summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// Gets the context items.
    /// </summary>
    public IDictionary<string, object?> Items { get; }

    /// <summary>
    /// Gets the required service.
    /// </summary>
    public T GetRequiredService<T>() where T : notnull => Services.GetRequiredService<T>();
}
