using Telegram.Bot.Types.Enums;

namespace TelegramBotKit.Options;

public sealed class WebhookOptions
{
    /// <summary>
    /// Публичный базовый URL вашего приложения (без пути).
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Путь endpoint-а в ASP.NET, куда Telegram будет присылать апдейты.
    /// Пример: "/telegram/update"
    /// </summary>
    public string Path { get; set; } = "/telegram/update";

    /// <summary>
    /// Если true — при установке webhook Telegram сбросит (drop) все pending updates.
    /// </summary>
    public bool DropPendingUpdates { get; set; } = false;

    /// <summary>
    /// Максимальное число соединений (1..100) для webhook.
    /// Если null — не задаём.
    /// </summary>
    public int? MaxConnections { get; set; } = null;

    /// <summary>
    /// Секретный токен для проверки, что запрос реально пришёл от Telegram
    /// (header: X-Telegram-Bot-Api-Secret-Token).
    /// </summary>
    public string? SecretToken { get; set; } = null;

    /// <summary>
    /// Какие типы апдейтов принимать.
    /// Пустой массив означает "все".
    /// </summary>
    public UpdateType[] AllowedUpdates { get; set; } = Array.Empty<UpdateType>();
}
