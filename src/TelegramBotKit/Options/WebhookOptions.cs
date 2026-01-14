using Telegram.Bot.Types.Enums;

namespace TelegramBotKit.Options;

public sealed class WebhookOptions
{
    /// <summary>
    /// Gets or sets the url.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the path.
    /// </summary>
    public string Path { get; set; } = "/telegram/update";

    /// <summary>
    /// Gets or sets a value indicating whether drop pending updates is enabled.
    /// </summary>
    public bool DropPendingUpdates { get; set; } = false;

    /// <summary>
    /// Gets or sets the max connections.
    /// </summary>
    public int? MaxConnections { get; set; } = null;

    /// <summary>
    /// Gets or sets the secret token.
    /// </summary>
    public string? SecretToken { get; set; } = null;

    /// <summary>
    /// Gets or sets the allowed update types.
    /// </summary>
    public UpdateType[] AllowedUpdates { get; set; } = Array.Empty<UpdateType>();
}
