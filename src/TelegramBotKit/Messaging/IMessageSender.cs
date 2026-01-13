using Telegram.Bot;
using Telegram.Bot.Types;

namespace TelegramBotKit.Messaging;

public interface IMessageSender
{
    Task<Message> SendText(long chatId, SendText msg, CancellationToken ct = default);

    Task<Message> ReplyText(Message replyTo, SendText msg, CancellationToken ct = default);

    Task<Message> EditText(long chatId, int messageId, EditText edit, CancellationToken ct = default);

    Task AnswerCallback(string callbackQueryId, AnswerCallback answer, CancellationToken ct = default);
}
