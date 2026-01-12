using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramBotKit.Messaging
{
    public interface IMessageSender
    {
        Task DeleteAsync(ChatId chatId, int messageId, CancellationToken ct = default);
        Task<Message> EditReplyMarkupAsync(ChatId chatId, int messageId, InlineKeyboardMarkup? replyMarkup = null, CancellationToken ct = default);
        Task<Message> EditTextAsync(ChatId chatId, int messageId, OutgoingMessage msg, CancellationToken ct = default);
        Task<Message> SendAsync(ChatId chatId, OutgoingMessage msg, ReplyParameters? replyParameters = null, CancellationToken ct = default);
    }
}