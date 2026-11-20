using Microsoft.Extensions.Options;
using TelegramBotKit.Options;

namespace TelegramBotKit.Webhook.AspNetCore;

internal sealed class WebhookOptionsValidator : IValidateOptions<WebhookOptions>
{
    public ValidateOptionsResult Validate(string? name, WebhookOptions options)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(options.Path) || !options.Path.StartsWith('/') ||
            options.Path.IndexOfAny(['?', '#', '{', '}']) >= 0 || options.Path.Any(char.IsWhiteSpace))
            errors.Add("Webhook Path must be an absolute, literal application path without query or fragment.");
        if (string.IsNullOrEmpty(options.SecretToken) || options.SecretToken.Length > 256 ||
            options.SecretToken.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('_' or '-')))
            errors.Add("Webhook SecretToken must contain 1-256 ASCII letters, digits, underscores or hyphens.");
        if (!string.IsNullOrEmpty(options.Url) && (!Uri.TryCreate(options.Url, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps || uri.Fragment.Length != 0 || uri.UserInfo.Length != 0))
            errors.Add("Webhook Url, when supplied, must be an absolute HTTPS URL without credentials or fragment.");
        if (options.MaxConnections is < 1 or > 100)
            errors.Add("Webhook MaxConnections must be between 1 and 100.");
        if (options.MaxRequestBodyBytes <= 0)
            errors.Add("Webhook MaxRequestBodyBytes must be greater than zero.");
        if (options.AllowedUpdates is null)
            errors.Add("Webhook AllowedUpdates must not be null.");
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
