using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace TelegramBotKit;

/// <summary>
/// Контекст обработки одного апдейта
/// </summary>
public sealed class BotContext
{
    public BotContext(
        Update update,
        ITelegramBotClient botClient,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        Update = update ?? throw new ArgumentNullException(nameof(update));
        BotClient = botClient ?? throw new ArgumentNullException(nameof(botClient));
        Services = services ?? throw new ArgumentNullException(nameof(services));
        CancellationToken = cancellationToken;
        Items = new Dictionary<string, object?>(StringComparer.Ordinal);
    }

    /// <summary>
    /// Сырой апдейт от Telegram (в нём может быть Message, CallbackQuery и т.д.)
    /// </summary>
    public Update Update { get; }

    /// <summary>
    /// Клиент Telegram (низкоуровневый).
    /// </summary>
    public ITelegramBotClient BotClient { get; }

    /// <summary>
    /// DI scope provider для обработки данного апдейта.
    /// </summary>
    public IServiceProvider Services { get; }

    /// <summary>
    /// Токен отмены (остановка приложения/таймауты).
    /// </summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// Карман для обмена данными между middleware/обработчиками.
    /// </summary>
    public IDictionary<string, object?> Items { get; }

    public T GetRequiredService<T>() where T : notnull => Services.GetRequiredService<T>();
}
