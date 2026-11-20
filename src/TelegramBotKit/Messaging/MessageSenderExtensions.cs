using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramBotKit.Messaging;

/// <summary>
/// Derives chat, message and thread identifiers from messages and callback queries for <see cref="IMessageSender"/>.
/// </summary>
/// <remarks>
/// Message-based sends inherit the source thread unless the request overrides it.
/// Operations requiring a chat message reject inline callbacks; Try-prefixed overloads
/// return false when the callback has no message. Await the returned task to observe request failure.
/// </remarks>
public static class MessageSenderExtensions
{
    extension(IMessageSender sender)
    {

        /// <summary>Send text to the same chat and, unless explicitly overridden, thread as <paramref name="message"/>.</summary>
        /// <param name="message">The Telegram message to process or use as the operation's target.</param>
        /// <param name="msg">The outgoing message content and delivery options.</param>
        /// <param name="ct">The token used to cancel the operation.</param>
        /// <returns>A task returning the message sent by Telegram.</returns>
        public Task<Message> SendText(Message message, SendText msg, CancellationToken ct = default)
            => sender.SendText(message.Chat.Id, msg with { MessageThreadId = msg.MessageThreadId ?? message.MessageThreadId }, ct);

        /// <summary>Send photo to the same chat and, unless explicitly overridden, thread as <paramref name="message"/>.</summary>
        /// <param name="message">The Telegram message to process or use as the operation's target.</param>
        /// <param name="msg">The outgoing message content and delivery options.</param>
        /// <param name="ct">The token used to cancel the operation.</param>
        /// <returns>A task returning the message sent by Telegram.</returns>
        public Task<Message> SendPhoto(Message message, SendPhoto msg, CancellationToken ct = default)
            => sender.SendPhoto(message.Chat.Id, msg with { MessageThreadId = msg.MessageThreadId ?? message.MessageThreadId }, ct);

        /// <summary>Edit the given <paramref name="message"/> text (by chatId + messageId).</summary>
        /// <param name="message">The Telegram message to process or use as the operation's target.</param>
        /// <param name="edit">The replacement message content and formatting options.</param>
        /// <param name="ct">The token used to cancel the operation.</param>
        /// <returns>A task returning the edited Telegram message.</returns>
        public Task<Message> EditText(Message message, EditText edit, CancellationToken ct = default)
            => sender.EditText(message.Chat.Id, message.Id, edit, ct);

        /// <summary>Edit reply markup of the given <paramref name="message"/> (by chatId + messageId).</summary>
        /// <param name="message">The Telegram message to process or use as the operation's target.</param>
        /// <param name="keyboard">The replacement inline keyboard, or null to remove it.</param>
        /// <param name="ct">The token used to cancel the operation.</param>
        /// <returns>The asynchronous operation, which completes after processing finishes.</returns>
        public Task EditReplyMarkup(Message message, InlineKeyboardMarkup? keyboard, CancellationToken ct = default)
            => sender.EditReplyMarkup(message.Chat.Id, message.Id, keyboard, ct);

        /// <summary>Edit the given <paramref name="message"/> photo/media (by chatId + messageId).</summary>
        /// <param name="message">The Telegram message to process or use as the operation's target.</param>
        /// <param name="edit">The replacement message content and formatting options.</param>
        /// <param name="ct">The token used to cancel the operation.</param>
        /// <returns>The asynchronous operation, which completes after processing finishes.</returns>
        public Task EditPhoto(Message message, EditPhoto edit, CancellationToken ct = default)
            => sender.EditPhoto(message.Chat.Id, message.Id, edit, ct);



        /// <summary>Answer a callback query.</summary>
        /// <param name="callback">The incoming callback query.</param>
        /// <param name="answer">The callback notification text, alert and optional URL.</param>
        /// <param name="ct">The token used to cancel the operation.</param>
        /// <returns>The asynchronous operation, which completes after processing finishes.</returns>
        public Task AnswerCallback(CallbackQuery callback, AnswerCallback answer, CancellationToken ct = default)
            => sender.AnswerCallback(callback.Id, answer, ct);

        /// <summary>
        /// Send text to the chat and, unless explicitly overridden, thread where the callback originated.
        /// Throws when <see cref="CallbackQuery.Message"/> is null (inline callbacks).
        /// </summary>
        /// <param name="callback">The incoming callback query.</param>
        /// <param name="msg">The outgoing message content and delivery options.</param>
        /// <param name="ct">The token used to cancel the operation.</param>
        /// <returns>A task returning the message sent by Telegram.</returns>
        public Task<Message> SendText(CallbackQuery callback, SendText msg, CancellationToken ct = default)
            => sender.SendText(RequireCallbackMessage(callback), msg, ct);

        /// <summary>
        /// Send a photo to the callback message's chat and, unless explicitly overridden, thread.
        /// Throws when <see cref="CallbackQuery.Message"/> is null (inline callbacks).
        /// </summary>
        /// <param name="callback">The incoming callback query.</param>
        /// <param name="msg">The outgoing message content and delivery options.</param>
        /// <param name="ct">The token used to cancel the operation.</param>
        /// <returns>A task returning the message sent by Telegram.</returns>
        public Task<Message> SendPhoto(CallbackQuery callback, SendPhoto msg, CancellationToken ct = default)
            => sender.SendPhoto(RequireCallbackMessage(callback), msg, ct);

        /// <summary>
        /// Reply with a photo to the callback message, inheriting its thread unless explicitly overridden.
        /// Throws when <see cref="CallbackQuery.Message"/> is null (inline callbacks).
        /// </summary>
        /// <param name="callback">The incoming callback query.</param>
        /// <param name="msg">The outgoing message content and delivery options.</param>
        /// <param name="ct">The token used to cancel the operation.</param>
        /// <returns>A task returning the message sent by Telegram.</returns>
        public Task<Message> ReplyPhoto(CallbackQuery callback, SendPhoto msg, CancellationToken ct = default)
            => sender.ReplyPhoto(RequireCallbackMessage(callback), msg, ct);

        /// <summary>Try to send a photo in the callback message's chat and thread. Returns false and a null task for inline callbacks.</summary>
        /// <param name="callback">The incoming callback query.</param>
        /// <param name="msg">The outgoing message content and delivery options.</param>
        /// <param name="task">The started operation, or null when the callback has no message. Await it to observe completion or failure.</param>
        /// <param name="ct">The token used to cancel the operation.</param>
        /// <returns>True if an operation was created; false if the callback contains no message. This does not indicate request success.</returns>
        public bool TrySendPhoto(CallbackQuery callback, SendPhoto msg, out Task<Message>? task, CancellationToken ct = default)
        {
            task = callback.Message is { } message ? sender.SendPhoto(message, msg, ct) : null;
            return task is not null;
        }

        /// <summary>Try to reply with a photo, inheriting the callback message's thread. Returns false and a null task for inline callbacks.</summary>
        /// <param name="callback">The incoming callback query.</param>
        /// <param name="msg">The outgoing message content and delivery options.</param>
        /// <param name="task">The started operation, or null when the callback has no message. Await it to observe completion or failure.</param>
        /// <param name="ct">The token used to cancel the operation.</param>
        /// <returns>True if an operation was created; false if the callback contains no message. This does not indicate request success.</returns>
        public bool TryReplyPhoto(CallbackQuery callback, SendPhoto msg, out Task<Message>? task, CancellationToken ct = default)
        {
            task = callback.Message is { } message ? sender.ReplyPhoto(message, msg, ct) : null;
            return task is not null;
        }

        /// <summary>
        /// Reply to the message from which the callback originated.
        /// Throws when <see cref="CallbackQuery.Message"/> is null (inline callbacks).
        /// </summary>
        /// <param name="callback">The incoming callback query.</param>
        /// <param name="msg">The outgoing message content and delivery options.</param>
        /// <param name="ct">The token used to cancel the operation.</param>
        /// <returns>A task returning the message sent by Telegram.</returns>
        public Task<Message> ReplyText(CallbackQuery callback, SendText msg, CancellationToken ct = default)
            => sender.ReplyText(RequireCallbackMessage(callback), msg, ct);

        /// <summary>
        /// Edit text of the message from which the callback originated.
        /// Throws when <see cref="CallbackQuery.Message"/> is null (inline callbacks).
        /// </summary>
        /// <param name="callback">The incoming callback query.</param>
        /// <param name="edit">The replacement message content and formatting options.</param>
        /// <param name="ct">The token used to cancel the operation.</param>
        /// <returns>A task returning the edited Telegram message.</returns>
        public Task<Message> EditText(CallbackQuery callback, EditText edit, CancellationToken ct = default)
        {
            var m = RequireCallbackMessage(callback);
            return sender.EditText(m.Chat.Id, m.Id, edit, ct);
        }

        /// <summary>
        /// Edit reply markup of the message from which the callback originated.
        /// Throws when <see cref="CallbackQuery.Message"/> is null (inline callbacks).
        /// </summary>
        /// <param name="callback">The incoming callback query.</param>
        /// <param name="keyboard">The replacement inline keyboard, or null to remove it.</param>
        /// <param name="ct">The token used to cancel the operation.</param>
        /// <returns>The asynchronous operation, which completes after processing finishes.</returns>
        public Task EditReplyMarkup(CallbackQuery callback, InlineKeyboardMarkup? keyboard, CancellationToken ct = default)
        {
            var m = RequireCallbackMessage(callback);
            return sender.EditReplyMarkup(m.Chat.Id, m.Id, keyboard, ct);
        }

        /// <summary>
        /// Edit photo/media of the message from which the callback originated.
        /// Throws when <see cref="CallbackQuery.Message"/> is null (inline callbacks).
        /// </summary>
        /// <param name="callback">The incoming callback query.</param>
        /// <param name="edit">The replacement message content and formatting options.</param>
        /// <param name="ct">The token used to cancel the operation.</param>
        /// <returns>The asynchronous operation, which completes after processing finishes.</returns>
        public Task EditPhoto(CallbackQuery callback, EditPhoto edit, CancellationToken ct = default)
        {
            var m = RequireCallbackMessage(callback);
            return sender.EditPhoto(m.Chat.Id, m.Id, edit, ct);
        }

        /// <summary>
        /// Tries to send text to the chat containing the callback message. Inline callbacks have no
        /// <see cref="CallbackQuery.Message"/>, so this returns false and sets <paramref name="task"/> to null.
        /// </summary>
        /// <param name="callback">The callback whose message identifies the destination chat.</param>
        /// <param name="msg">The text request to send.</param>
        /// <param name="task">The send operation, or null when the callback has no message.</param>
        /// <param name="ct">A token that cancels the Telegram request.</param>
        /// <returns>True when the operation was created; false for an inline callback without a message.</returns>
        public bool TrySendText(CallbackQuery callback, SendText msg, out Task<Message>? task, CancellationToken ct = default)
        {
            if (callback.Message is null)
            {
                task = null;
                return false;
            }

            task = sender.SendText(callback.Message, msg, ct);
            return true;
        }

        /// <summary>
        /// Tries to reply to the callback message. Inline callbacks have no
        /// <see cref="CallbackQuery.Message"/>, so this returns false and sets <paramref name="task"/> to null.
        /// </summary>
        /// <param name="callback">The callback whose message will be replied to.</param>
        /// <param name="msg">The text request to send as a reply.</param>
        /// <param name="task">The send operation, or null when the callback has no message.</param>
        /// <param name="ct">A token that cancels the Telegram request.</param>
        /// <returns>True when the operation was created; false for an inline callback without a message.</returns>
        public bool TryReplyText(CallbackQuery callback, SendText msg, out Task<Message>? task, CancellationToken ct = default)
        {
            if (callback.Message is null)
            {
                task = null;
                return false;
            }

            task = sender.ReplyText(callback.Message, msg, ct);
            return true;
        }

        /// <summary>
        /// Tries to edit the callback message text. Inline callbacks have no
        /// <see cref="CallbackQuery.Message"/>, so this returns false and sets <paramref name="task"/> to null.
        /// </summary>
        /// <param name="callback">The callback whose message will be edited.</param>
        /// <param name="edit">The text edit request.</param>
        /// <param name="task">The edit operation, or null when the callback has no message.</param>
        /// <param name="ct">A token that cancels the Telegram request.</param>
        /// <returns>True when the operation was created; false for an inline callback without a message.</returns>
        public bool TryEditText(CallbackQuery callback, EditText edit, out Task<Message>? task, CancellationToken ct = default)
        {
            if (callback.Message is null)
            {
                task = null;
                return false;
            }

            task = sender.EditText(callback.Message.Chat.Id, callback.Message.Id, edit, ct);
            return true;
        }

        /// <summary>
        /// Tries to edit the callback message keyboard. Inline callbacks have no
        /// <see cref="CallbackQuery.Message"/>, so this returns false and sets <paramref name="task"/> to null.
        /// </summary>
        /// <param name="callback">The callback whose message markup will be edited.</param>
        /// <param name="keyboard">The replacement keyboard, or null to remove it.</param>
        /// <param name="task">The edit operation, or null when the callback has no message.</param>
        /// <param name="ct">A token that cancels the Telegram request.</param>
        /// <returns>True when the operation was created; false for an inline callback without a message.</returns>
        public bool TryEditReplyMarkup(CallbackQuery callback, InlineKeyboardMarkup? keyboard, out Task? task, CancellationToken ct = default)
        {
            if (callback.Message is null)
            {
                task = null;
                return false;
            }

            task = sender.EditReplyMarkup(callback.Message.Chat.Id, callback.Message.Id, keyboard, ct);
            return true;
        }

        /// <summary>
        /// Tries to edit the callback message photo. Inline callbacks have no
        /// <see cref="CallbackQuery.Message"/>, so this returns false and sets <paramref name="task"/> to null.
        /// </summary>
        /// <param name="callback">The callback whose message photo will be edited.</param>
        /// <param name="edit">The media edit request.</param>
        /// <param name="task">The edit operation, or null when the callback has no message.</param>
        /// <param name="ct">A token that cancels the Telegram request.</param>
        /// <returns>True when the operation was created; false for an inline callback without a message.</returns>
        public bool TryEditPhoto(CallbackQuery callback, EditPhoto edit, out Task? task, CancellationToken ct = default)
        {
            if (callback.Message is null)
            {
                task = null;
                return false;
            }

            task = sender.EditPhoto(callback.Message.Chat.Id, callback.Message.Id, edit, ct);
            return true;
        }
    }



    /// <summary>
    /// Try to get the message from a callback query. Returns false for inline callbacks.
    /// </summary>
    /// <param name="callback">The incoming callback query.</param>
    /// <param name="message">The callback's message, or null when this method returns false.</param>
    /// <returns>True if the callback contains a message; false for an inline callback.</returns>
    public static bool TryGetMessage(this CallbackQuery callback, out Message message)
    {
        message = callback.Message!;
        return callback.Message is not null;
    }

    private static Message RequireCallbackMessage(CallbackQuery callback)
    {
        if (callback is null) throw new ArgumentNullException(nameof(callback));

        // Inline callbacks have no chat message; these helpers require chat and message identifiers.
        return callback.Message ?? throw new NotSupportedException(
            "CallbackQuery.Message is null (inline callback). " +
            "This operation requires a message in a chat (chatId + messageId)." );
    }
}
