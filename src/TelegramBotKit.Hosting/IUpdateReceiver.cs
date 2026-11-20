using Telegram.Bot.Types;

namespace TelegramBotKit.Hosting;

/// <summary>Accepts updates from a transport for independent background processing.</summary>
/// <remarks>
/// Successful completion means admission, not completion of handlers. The default receiver
/// tracks work in memory and does not provide durable storage, deduplication or retries.
/// Implementations must state their admission guarantees and manage execution lifetime independently.
/// </remarks>
public interface IUpdateReceiver
{
    /// <summary>Accepts an update without waiting for its handlers to complete.</summary>
    /// <param name="update">The fully deserialized update. Do not modify it after admission.</param>
    /// <param name="ct">Cancels admission only; accepted work must outlive the transport request.</param>
    /// <returns>A task completed after admission, or failed if the update could not be accepted.</returns>
    ValueTask ReceiveAsync(Update update, CancellationToken ct = default);
}
