using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramBotKit.Messaging;

/// <summary>
/// Sends, edits and replies to chat messages and acknowledges callback queries.
/// </summary>
public interface IMessageSender
{
    /// <summary>
    /// Sends text to the specified chat and the optional thread in the request.
    /// </summary>
    /// <param name="chatId">The destination chat identifier.</param>
    /// <param name="msg">The outgoing message content and delivery options.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>A task returning the message sent by Telegram.</returns>
    Task<Message> SendText(long chatId, SendText msg, CancellationToken ct = default);

    /// <summary>
    /// Replies with text, inheriting the source thread unless the request specifies one.
    /// </summary>
    /// <param name="replyTo">The message to reply to; its thread is inherited unless the request overrides it.</param>
    /// <param name="msg">The outgoing message content and delivery options.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>A task returning the message sent by Telegram.</returns>
    Task<Message> ReplyText(Message replyTo, SendText msg, CancellationToken ct = default);

    /// <summary>
    /// Sends a photo to the specified chat and the optional thread in the request.
    /// </summary>
    /// <param name="chatId">The destination chat identifier.</param>
    /// <param name="msg">The outgoing message content and delivery options.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>A task returning the message sent by Telegram.</returns>
    Task<Message> SendPhoto(long chatId, SendPhoto msg, CancellationToken ct = default);

    /// <summary>
    /// Replaces or removes an existing message's inline keyboard.
    /// </summary>
    /// <param name="chatId">The destination chat identifier.</param>
    /// <param name="messageId">The identifier of the message to edit.</param>
    /// <param name="keyboard">The replacement inline keyboard, or null to remove it.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>The asynchronous operation, which completes after processing finishes.</returns>
    Task EditReplyMarkup(long chatId, int messageId, InlineKeyboardMarkup? keyboard, CancellationToken ct = default);

    /// <summary>
    /// Replaces an existing message's photo and optional caption.
    /// </summary>
    /// <param name="chatId">The destination chat identifier.</param>
    /// <param name="messageId">The identifier of the message to edit.</param>
    /// <param name="edit">The replacement message content and formatting options.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>The asynchronous operation, which completes after processing finishes.</returns>
    Task EditPhoto(
        long chatId,
        int messageId,
        EditPhoto edit,
        CancellationToken ct = default);


    /// <summary>
    /// Replies with a photo, inheriting the source thread unless the request specifies one.
    /// </summary>
    /// <param name="replyTo">The message to reply to; its thread is inherited unless the request overrides it.</param>
    /// <param name="msg">The outgoing message content and delivery options.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>A task returning the message sent by Telegram.</returns>
    Task<Message> ReplyPhoto(Message replyTo, SendPhoto msg, CancellationToken ct = default);

    /// <summary>
    /// Replaces an existing message's text, formatting and optional inline keyboard.
    /// </summary>
    /// <param name="chatId">The destination chat identifier.</param>
    /// <param name="messageId">The identifier of the message to edit.</param>
    /// <param name="edit">The replacement message content and formatting options.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>A task returning the edited Telegram message.</returns>
    Task<Message> EditText(long chatId, int messageId, EditText edit, CancellationToken ct = default);

    /// <summary>
    /// Acknowledges a callback query, optionally displaying a notification or alert.
    /// </summary>
    /// <param name="callbackQueryId">The identifier of the callback query to acknowledge.</param>
    /// <param name="answer">The callback notification text, alert and optional URL.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>The asynchronous operation, which completes after processing finishes.</returns>
    Task AnswerCallback(string callbackQueryId, AnswerCallback answer, CancellationToken ct = default);
}
