using Microsoft.Extensions.Options;

namespace TelegramBotKit.Options;

/// <summary>
/// Валидация опций. Регистрируется через services.AddOptions&lt;TelegramBotKitOptions&gt;().ValidateOnStart()
/// </summary>
internal sealed class TelegramBotKitOptionsValidator : IValidateOptions<TelegramBotKitOptions>
{
    public ValidateOptionsResult Validate(string? name, TelegramBotKitOptions options)
    {
        if (options is null)
            return ValidateOptionsResult.Fail("Options is null.");

        if (string.IsNullOrWhiteSpace(options.Token))
            return ValidateOptionsResult.Fail("TelegramBotKitOptions.Token is required.");

        if (options.Mode == UpdateDeliveryMode.Webhook)
        {
            if (options.Webhook.Url is null)
                return ValidateOptionsResult.Fail("Webhook mode requires TelegramBotKitOptions.Webhook.Url.");
            if (!options.Webhook.Path.StartsWith('/'))
                return ValidateOptionsResult.Fail("Webhook.Path must start with '/'.");
            if (options.Webhook.MaxConnections is < 1 or > 100)
                return ValidateOptionsResult.Fail("Webhook.MaxConnections must be between 1 and 100.");
        }

        if (options.Mode == UpdateDeliveryMode.Polling)
        {
            if (options.Polling.MaxDegreeOfParallelism < 1)
                return ValidateOptionsResult.Fail("Polling.MaxDegreeOfParallelism must be >= 1.");
        }

        return ValidateOptionsResult.Success;
    }
}
