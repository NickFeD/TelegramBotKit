using Microsoft.Extensions.Options;

namespace TelegramBotKit.Options;

internal sealed class TelegramBotKitOptionsValidator : IValidateOptions<TelegramBotKitOptions>
{
    public ValidateOptionsResult Validate(string? name, TelegramBotKitOptions options)
    {
        if (options is null)
            return ValidateOptionsResult.Fail("Options instance is null.");

        if (string.IsNullOrWhiteSpace(options.Token))
            return ValidateOptionsResult.Fail("TelegramBotKitOptions.Token is required.");

        if (options.Polling is null)
            return ValidateOptionsResult.Fail("TelegramBotKitOptions.Polling is required.");

        if (options.Polling.Limit is < 1 or > 100)
            return ValidateOptionsResult.Fail("TelegramBotKitOptions.Polling.Limit must be between 1 and 100.");

        if (options.Polling.TimeoutSeconds is < 0 or > 60)
            return ValidateOptionsResult.Fail("TelegramBotKitOptions.Polling.TimeoutSeconds must be between 0 and 60.");

        if (options.Polling.MaxDegreeOfParallelism < 0)
            return ValidateOptionsResult.Fail("TelegramBotKitOptions.Polling.MaxDegreeOfParallelism must be >= 0.");

        return ValidateOptionsResult.Success;
    }
}
