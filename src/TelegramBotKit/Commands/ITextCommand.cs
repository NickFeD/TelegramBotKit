using Telegram.Bot.Types;

namespace TelegramBotKit.Commands;

/// <summary>
/// Defines the contract for a text command.
/// </summary>
public interface ITextCommand : ICommand
{
    /// <summary>
    /// Handles the update asynchronously.
    /// </summary>
    Task HandleAsync(Message message, BotContext ctx);
}
