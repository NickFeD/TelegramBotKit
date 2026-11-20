using Telegram.Bot.Types;

namespace TelegramBotKit.Commands;

/// <summary>
/// Handles a message whose leading slash-command token matches a registered key.
/// </summary>
public interface IMessageCommand : ICommand
{
    /// <summary>
    /// Processes the original message after its slash-command key has been matched.
    /// </summary>
    /// <param name="message">The Telegram message to process or use as the operation's target.</param>
    /// <param name="ctx">The current update context, including scoped services and cancellation.</param>
    /// <returns>The asynchronous operation, which completes after processing finishes.</returns>
    Task HandleAsync(Message message, BotContext ctx);
}
