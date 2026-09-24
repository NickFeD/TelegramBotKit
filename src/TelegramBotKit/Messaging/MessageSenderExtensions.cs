using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramBotKit.Messaging;

/// <summary>
/// Opt-in convenience overloads for <see cref="IMessageSender"/> that derive chat/message identifiers
/// from Telegram payload types (<see cref="Message"/>, <see cref="CallbackQuery"/>).
/// 
/// Design goals:
/// - predictable (no Update-type guessing)
/// - minimal logic (only direct field extraction)
/// - safe for future update types (user can always call base API with explicit ids)
/// </summary>
public static class MessageSenderExtensions
{
    extension(IMessageSender sender)
    {
        // -------------------------
        // Message-based
        // -------------------------

        /// <summary>Send text to the same chat and, unless explicitly overridden, thread as <paramref name="message"/>.</summary>
        public Task<Message> SendText(Message message, SendText msg, CancellationToken ct = default)
            => sender.SendText(message.Chat.Id, msg with { MessageThreadId = msg.MessageThreadId ?? message.MessageThreadId }, ct);

        /// <summary>Send photo to the same chat and, unless explicitly overridden, thread as <paramref name="message"/>.</summary>
        public Task<Message> SendPhoto(Message message, SendPhoto msg, CancellationToken ct = default)
            => sender.SendPhoto(message.Chat.Id, msg with { MessageThreadId = msg.MessageThreadId ?? message.MessageThreadId }, ct);

        /// <summary>Edit the given <paramref name="message"/> text (by chatId + messageId).</summary>
        public Task<Message> EditText(Message message, EditText edit, CancellationToken ct = default)
            => sender.EditText(message.Chat.Id, message.Id, edit, ct);

        /// <summary>Edit reply markup of the given <paramref name="message"/> (by chatId + messageId).</summary>
        public Task EditReplyMarkup(Message message, InlineKeyboardMarkup? keyboard, CancellationToken ct = default)
            => sender.EditReplyMarkup(message.Chat.Id, message.Id, keyboard, ct);

        /// <summary>Edit the given <paramref name="message"/> photo/media (by chatId + messageId).</summary>
        public Task EditPhoto(Message message, EditPhoto edit, CancellationToken ct = default)
            => sender.EditPhoto(message.Chat.Id, message.Id, edit, ct);


        // -------------------------
        // CallbackQuery-based
        // -------------------------

        /// <summary>Answer a callback query.</summary>
        public Task AnswerCallback(CallbackQuery callback, AnswerCallback answer, CancellationToken ct = default)
            => sender.AnswerCallback(callback.Id, answer, ct);

        /// <summary>
        /// Send text to the chat and, unless explicitly overridden, thread where the callback originated.
        /// Throws when <see cref="CallbackQuery.Message"/> is null (inline callbacks).
        /// </summary>
        public Task<Message> SendText(CallbackQuery callback, SendText msg, CancellationToken ct = default)
            => sender.SendText(RequireCallbackMessage(callback), msg, ct);

        /// <summary>
        /// Send a photo to the callback message's chat and, unless explicitly overridden, thread.
        /// Throws when <see cref="CallbackQuery.Message"/> is null (inline callbacks).
        /// </summary>
        public Task<Message> SendPhoto(CallbackQuery callback, SendPhoto msg, CancellationToken ct = default)
            => sender.SendPhoto(RequireCallbackMessage(callback), msg, ct);

        /// <summary>
        /// Reply with a photo to the callback message, inheriting its thread unless explicitly overridden.
        /// Throws when <see cref="CallbackQuery.Message"/> is null (inline callbacks).
        /// </summary>
        public Task<Message> ReplyPhoto(CallbackQuery callback, SendPhoto msg, CancellationToken ct = default)
            => sender.ReplyPhoto(RequireCallbackMessage(callback), msg, ct);

        /// <summary>Try to send a photo in the callback message's chat and thread. Returns false and a null task for inline callbacks.</summary>
        public bool TrySendPhoto(CallbackQuery callback, SendPhoto msg, out Task<Message>? task, CancellationToken ct = default)
        {
            task = callback.Message is { } message ? sender.SendPhoto(message, msg, ct) : null;
            return task is not null;
        }

        /// <summary>Try to reply with a photo, inheriting the callback message's thread. Returns false and a null task for inline callbacks.</summary>
        public bool TryReplyPhoto(CallbackQuery callback, SendPhoto msg, out Task<Message>? task, CancellationToken ct = default)
        {
            task = callback.Message is { } message ? sender.ReplyPhoto(message, msg, ct) : null;
            return task is not null;
        }

        /// <summary>
        /// Reply to the message from which the callback originated.
        /// Throws when <see cref="CallbackQuery.Message"/> is null (inline callbacks).
        /// </summary>
        public Task<Message> ReplyText(CallbackQuery callback, SendText msg, CancellationToken ct = default)
            => sender.ReplyText(RequireCallbackMessage(callback), msg, ct);

        /// <summary>
        /// Edit text of the message from which the callback originated.
        /// Throws when <see cref="CallbackQuery.Message"/> is null (inline callbacks).
        /// </summary>
        public Task<Message> EditText(CallbackQuery callback, EditText edit, CancellationToken ct = default)
        {
            var m = RequireCallbackMessage(callback);
            return sender.EditText(m.Chat.Id, m.Id, edit, ct);
        }

        /// <summary>
        /// Edit reply markup of the message from which the callback originated.
        /// Throws when <see cref="CallbackQuery.Message"/> is null (inline callbacks).
        /// </summary>
        public Task EditReplyMarkup(CallbackQuery callback, InlineKeyboardMarkup? keyboard, CancellationToken ct = default)
        {
            var m = RequireCallbackMessage(callback);
            return sender.EditReplyMarkup(m.Chat.Id, m.Id, keyboard, ct);
        }

        /// <summary>
        /// Edit photo/media of the message from which the callback originated.
        /// Throws when <see cref="CallbackQuery.Message"/> is null (inline callbacks).
        /// </summary>
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


    // -------------------------
    // Try* variants for inline safety
    // -------------------------

    /// <summary>
    /// Try to get the message from a callback query. Returns false for inline callbacks.
    /// </summary>
    public static bool TryGetMessage(this CallbackQuery callback, out Message message)
    {
        message = callback.Message!;
        return callback.Message is not null;
    }

    private static Message RequireCallbackMessage(CallbackQuery callback)
    {
        if (callback is null) throw new ArgumentNullException(nameof(callback));

        // Inline callbacks have Message == null. This library's IMessageSender currently
        // supports editing by (chatId, messageId) only, so we fail fast and loudly.
        return callback.Message ?? throw new NotSupportedException(
            "CallbackQuery.Message is null (inline callback). " +
            "This operation requires a message in a chat (chatId + messageId)." );
    }
}
