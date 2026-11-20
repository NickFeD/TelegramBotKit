using Telegram.Bot.Types.Enums;

namespace TelegramBotKit.Options;

/// <summary>
/// Configures ASP.NET Core webhook reception and explicit Telegram webhook management.
/// </summary>
public sealed class WebhookOptions
{
    /// <summary>Gets or sets the maximum webhook request body size in bytes. Defaults to one MiB.</summary>
    public int MaxRequestBodyBytes { get; set; } = 1024 * 1024;

    /// <summary>
    /// Gets or sets the public HTTPS URL sent to Telegram when explicitly registering a webhook.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the local POST endpoint path. Defaults to /telegram/update.
    /// </summary>
    public string Path { get; set; } = "/telegram/update";

    /// <summary>
    /// Gets or sets whether explicit webhook registration discards Telegram's pending updates.
    /// </summary>
    public bool DropPendingUpdates { get; set; } = false;

    /// <summary>
    /// Gets or sets Telegram's maximum simultaneous webhook connections (1–100), or null for its default.
    /// </summary>
    public int? MaxConnections { get; set; } = null;

    /// <summary>
    /// Gets or sets the required secret checked against X-Telegram-Bot-Api-Secret-Token.
    /// </summary>
    /// <remarks>Use 1–256 ASCII letters, digits, underscores or hyphens. Keep it outside source control.</remarks>
    public string? SecretToken { get; set; } = null;

    /// <summary>
    /// Gets or sets update types sent to Telegram during explicit webhook registration.
    /// </summary>
    /// <remarks>An empty array uses Telegram's default subscription; it does not add missing Core handlers.</remarks>
    public UpdateType[] AllowedUpdates { get; set; } = Array.Empty<UpdateType>();
}
