using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramBotKit.Messaging;

/// <summary>
/// Модель исходящего текстового сообщения (чтобы не путать с методом bot.SendMessage).
/// </summary>
public sealed record OutgoingMessage(
    string Text,
    ParseMode ParseMode = default,
    LinkPreviewOptions? LinkPreviewOptions = null,
    IEnumerable<MessageEntity>? Entities = null,
    ReplyMarkup? ReplyMarkup = null);

public sealed class MessageSender : IMessageSender
{
    private readonly ITelegramBotClient _bot;
    private readonly ILogger<MessageSender> _log;

    public MessageSender(ITelegramBotClient bot, ILogger<MessageSender> log)
    {
        _bot = bot;
        _log = log;
    }

    public Task<Message> SendAsync(
        ChatId chatId,
        OutgoingMessage msg,
        ReplyParameters? replyParameters = null,
        CancellationToken ct = default)
    {
        _log.LogDebug("SendMessage chat:{chatId} text:{text}", chatId, msg.Text);

        // В v22 параметры местами переставлялись — безопаснее использовать именованные аргументы.
        return _bot.SendMessage(
            chatId: chatId,
            text: msg.Text,
            parseMode: msg.ParseMode == default ? ParseMode.None : msg.ParseMode,
            entities: msg.Entities?.ToArray(),
            linkPreviewOptions: msg.LinkPreviewOptions,
            replyParameters: replyParameters,
            replyMarkup: msg.ReplyMarkup,
            cancellationToken: ct);
    }

    public Task<Message> EditTextAsync(
        ChatId chatId,
        int messageId,
        OutgoingMessage msg,
        CancellationToken ct = default)
    {
        _log.LogDebug("EditMessageText chat:{chatId} msg:{messageId} text:{text}", chatId, messageId, msg.Text);

        return _bot.EditMessageText(
            chatId: chatId,
            messageId: messageId,
            text: msg.Text,
            parseMode: msg.ParseMode == default ? ParseMode.None : msg.ParseMode,
            entities: msg.Entities?.ToArray(),
            linkPreviewOptions: msg.LinkPreviewOptions,
            replyMarkup: msg.ReplyMarkup as InlineKeyboardMarkup, // editMessageText принимает inline keyboard
            cancellationToken: ct);
    }

    public Task<Message> EditReplyMarkupAsync(
        ChatId chatId,
        int messageId,
        InlineKeyboardMarkup? replyMarkup = null,
        CancellationToken ct = default)
    {
        _log.LogDebug("EditMessageReplyMarkup chat:{chatId} msg:{messageId}", chatId, messageId);

        return _bot.EditMessageReplyMarkup(
            chatId: chatId,
            messageId: messageId,
            replyMarkup: replyMarkup,
            cancellationToken: ct);
    }

    public Task DeleteAsync(ChatId chatId, int messageId, CancellationToken ct = default)
    {
        _log.LogDebug("DeleteMessage chat:{chatId} msg:{messageId}", chatId, messageId);
        return _bot.DeleteMessage(chatId: chatId, messageId: messageId, cancellationToken: ct);
    }
}
