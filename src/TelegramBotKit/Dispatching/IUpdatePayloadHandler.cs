using Telegram.Bot.Types;

namespace TelegramBotKit.Dispatching;

/// <summary>
/// Defines the contract for an update payload handler.
/// </summary>
public interface IUpdatePayloadHandler<TPayload> where TPayload : class
{
    /// <summary>
    /// Handles the update asynchronously.
    /// </summary>
    Task HandleAsync(TPayload payload, BotContext ctx);
}

