using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramBotKit.Messaging;

public interface IMessageSender
{
    Task<Message> SendText(long chatId, SendText msg, CancellationToken ct = default);

    Task<Message> ReplyText(Message replyTo, SendText msg, CancellationToken ct = default);

    Task<Message> SendPhoto(long chatId, SendPhoto msg, CancellationToken ct = default);

    Task EditReplyMarkup(long chatId, int messageId, InlineKeyboardMarkup? keyboard, CancellationToken ct = default);

    Task EditPhoto(
        long chatId,
        int messageId,
        EditPhoto edit,
        CancellationToken ct = default);


    Task<Message> ReplyPhoto(Message replyTo, SendPhoto msg, CancellationToken ct = default);

    Task<Message> EditText(long chatId, int messageId, EditText edit, CancellationToken ct = default);

    Task AnswerCallback(string callbackQueryId, AnswerCallback answer, CancellationToken ct = default);
}
