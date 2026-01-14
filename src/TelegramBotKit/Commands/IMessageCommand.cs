using Telegram.Bot.Types;

namespace TelegramBotKit.Commands;

/// <summary>
/// Defines the contract for a message command.
/// </summary>
public interface IMessageCommand : ICommand
{
    /// <summary>
    /// Handles the update asynchronously.
    /// </summary>
    Task HandleAsync(Message message, BotContext ctx);
}
