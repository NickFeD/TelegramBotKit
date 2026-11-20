using Telegram.Bot.Types;

namespace TelegramBotKit.Commands;

/// <summary>
/// Handles a callback query whose first callback-data token matches a registered key.
/// </summary>
public interface ICallbackCommand : ICommand
{
    /// <summary>
    /// Processes the callback query and the arguments following its routing key.
    /// </summary>
    /// <param name="query">The incoming callback query.</param>
    /// <param name="args">The space-separated arguments following the callback routing key.</param>
    /// <param name="ctx">The current update context, including scoped services and cancellation.</param>
    /// <returns>The asynchronous operation, which completes after processing finishes.</returns>
    Task HandleAsync(CallbackQuery query, string[] args, BotContext ctx);
}
