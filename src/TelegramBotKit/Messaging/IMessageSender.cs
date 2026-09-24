using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramBotKit.Messaging;

/// <summary>
/// Defines the contract for a message sender.
/// </summary>
public interface IMessageSender
{
    /// <summary>
    /// Sends text to the chat and optional thread specified by the request.
    /// </summary>
    Task<Message> SendText(long chatId, SendText msg, CancellationToken ct = default);

    /// <summary>
    /// Replies with text, inheriting the source thread unless the request specifies one.
    /// </summary>
    Task<Message> ReplyText(Message replyTo, SendText msg, CancellationToken ct = default);

    /// <summary>
    /// Sends a photo to the chat and optional thread specified by the request.
    /// </summary>
    Task<Message> SendPhoto(long chatId, SendPhoto msg, CancellationToken ct = default);

    /// <summary>
    /// Edits the reply markup.
    /// </summary>
    Task EditReplyMarkup(long chatId, int messageId, InlineKeyboardMarkup? keyboard, CancellationToken ct = default);

    /// <summary>
    /// Edits the photo.
    /// </summary>
    Task EditPhoto(
        long chatId,
        int messageId,
        EditPhoto edit,
        CancellationToken ct = default);


    /// <summary>
    /// Replies with a photo, inheriting the source thread unless the request specifies one.
    /// </summary>
    Task<Message> ReplyPhoto(Message replyTo, SendPhoto msg, CancellationToken ct = default);

    /// <summary>
    /// Edits the text.
    /// </summary>
    Task<Message> EditText(long chatId, int messageId, EditText edit, CancellationToken ct = default);

    /// <summary>
    /// Answers the callback.
    /// </summary>
    Task AnswerCallback(string callbackQueryId, AnswerCallback answer, CancellationToken ct = default);
}
