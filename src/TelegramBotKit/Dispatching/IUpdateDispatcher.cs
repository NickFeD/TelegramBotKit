using Telegram.Bot.Types;

namespace TelegramBotKit.Dispatching;

/// <summary>
/// Defines the contract for an update dispatcher.
/// </summary>
public interface IUpdateDispatcher
{
    /// <summary>
    /// Dispatches the update asynchronously.
    /// </summary>
    Task DispatchAsync(Update update, CancellationToken ct = default);
}
