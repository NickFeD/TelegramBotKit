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

        /// <summary>Send text to the same chat as <paramref name="message"/>.</summary>
        public Task<Message> SendText(Message message, SendText msg, CancellationToken ct = default)
            => sender.SendText(message.Chat.Id, msg, ct);

        /// <summary>Send photo to the same chat as <paramref name="message"/>.</summary>
        public Task<Message> SendPhoto(Message message, SendPhoto msg, CancellationToken ct = default)
            => sender.SendPhoto(message.Chat.Id, msg, ct);

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
        /// Send text to the chat where the callback originated.
        /// Throws when <see cref="CallbackQuery.Message"/> is null (inline callbacks).
        /// </summary>
        public Task<Message> SendText(CallbackQuery callback, SendText msg, CancellationToken ct = default)
            => sender.SendText(RequireCallbackMessage(callback).Chat.Id, msg, ct);

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

        public bool TrySendText(CallbackQuery callback, SendText msg, out Task<Message>? task, CancellationToken ct = default)
        {
            if (callback.Message is null)
            {
                task = null;
                return false;
            }

            task = sender.SendText(callback.Message.Chat.Id, msg, ct);
            return true;
        }

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
