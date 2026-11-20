namespace TelegramBotKit.Hosting;

/// <summary>Configures shared execution for polling and webhook updates.</summary>
public sealed class UpdateProcessingOptions
{
    /// <summary>Gets or sets the maximum number of concurrently executing scheduled handlers.</summary>
    /// <remarks>
    /// A positive value limits concurrency; zero or a negative value removes the global limit.
    /// Null preserves the legacy TelegramBotKitOptions.Polling.MaxDegreeOfParallelism setting.
    /// Conversation response delivery bypasses this limit so waiting handlers can resume.
    /// This setting does not bound the number of queued updates.
    /// </remarks>
    public int? MaxDegreeOfParallelism { get; set; }
}
