using Telegram.Bot;
using Telegram.Bot.Types;

namespace TelegramBotKit.Messaging;

public sealed class MessageSender : IMessageSender
{
    private readonly ITelegramBotClient _bot;

    public MessageSender(ITelegramBotClient bot)
        => _bot = bot ?? throw new ArgumentNullException(nameof(bot));

    public Task<Message> SendText(long chatId, SendText msg, CancellationToken ct = default)
    {
        if (chatId == 0) throw new ArgumentOutOfRangeException(nameof(chatId));
        ValidateText(msg?.Text);

        return _bot.SendMessage(
            chatId: chatId,
            text: msg.Text,
            parseMode: msg.ParseMode,
            linkPreviewOptions: msg.LinkPreviewOptions,
            entities: msg.Entities,
            replyMarkup: msg.ReplyMarkup,
            disableNotification: msg.DisableNotification,
            protectContent: msg.ProtectContent,
            cancellationToken: ct);
    }

    public Task<Message> ReplyText(Message replyTo, SendText msg, CancellationToken ct = default)
    {
        if (replyTo is null) throw new ArgumentNullException(nameof(replyTo));
        ValidateText(msg?.Text);

        return _bot.SendMessage(
            chatId: replyTo.Chat.Id,
            text: msg.Text,
            parseMode: msg.ParseMode,
            linkPreviewOptions: msg.LinkPreviewOptions,
            entities: msg.Entities,
            replyParameters: new ReplyParameters { MessageId = replyTo.Id },
            replyMarkup: msg.ReplyMarkup,
            disableNotification: msg.DisableNotification,
            protectContent: msg.ProtectContent,
            cancellationToken: ct);
    }

    public Task<Message> EditText(long chatId, int messageId, EditText edit, CancellationToken ct = default)
    {
        if (chatId == 0) throw new ArgumentOutOfRangeException(nameof(chatId));
        if (messageId <= 0) throw new ArgumentOutOfRangeException(nameof(messageId));
        ValidateText(edit?.Text);

        return _bot.EditMessageText(
            chatId: chatId,
            messageId: messageId,
            text: edit.Text,
            parseMode: edit.ParseMode,
            linkPreviewOptions: edit.LinkPreviewOptions,
            entities: edit.Entities,
            replyMarkup: edit.ReplyMarkup,
            cancellationToken: ct);
    }

    public Task AnswerCallback(string callbackQueryId, AnswerCallback answer, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(callbackQueryId))
            throw new ArgumentException("callbackQueryId is required.", nameof(callbackQueryId));

        answer ??= new AnswerCallback();

        return _bot.AnswerCallbackQuery(
            callbackQueryId: callbackQueryId,
            text: answer.Text,
            showAlert: answer.ShowAlert,
            url: answer.Url,
            cancellationToken: ct);
    }

    private static void ValidateText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new TelegramBotKitConfigurationException("Message text is required (not null/empty).");
    }
}
