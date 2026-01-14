using Telegram.Bot.Types;

namespace TelegramBotKit.Commands;

/// <summary>
/// Defines the contract for a callback command.
/// </summary>
public interface ICallbackCommand : ICommand
{
    /// <summary>
    /// Handles the update asynchronously.
    /// </summary>
    Task HandleAsync(CallbackQuery query, string[] args, BotContext ctx);
}
