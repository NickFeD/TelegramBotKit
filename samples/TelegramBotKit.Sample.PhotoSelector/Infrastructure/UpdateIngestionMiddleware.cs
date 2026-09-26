using Telegram.Bot.Types.Enums;
using TelegramBotKit.Middleware;
using TelegramBotKit.Sample.PhotoSelector.Access;
using TelegramBotKit.Sample.PhotoSelector.Bot;
using TelegramBotKit.Sample.PhotoSelector.Data.Entities;
using TelegramBotKit.Sample.PhotoSelector.Media;

namespace TelegramBotKit.Sample.PhotoSelector.Infrastructure;

public sealed class UpdateIngestionMiddleware(
    AccessService accessService,
    MediaIndexService mediaIndexService,
    BotCommandMenuService commandMenu) : IUpdateMiddleware
{
    public async Task InvokeAsync(BotContext ctx, BotContextDelegate next)
    {
        var message = ctx.Update.Message;
        if (message is not null)
        {
            if (message.From is { } from)
            {
                var becameAdmin = await accessService.TryBootstrapAdminAsync(
                    from.Id,
                    message.Chat.Type == ChatType.Private,
                    !from.IsBot,
                    IsUserContentMessage(message.Type),
                    ctx.CancellationToken);
                if (becameAdmin)
                    await commandMenu.ConfigureUserAsync(from.Id, UserRole.Admin, ctx.CancellationToken);
            }

            if (message.ForumTopicCreated is { } topic && message.MessageThreadId is { } threadId)
            {
                await mediaIndexService.ObserveTopicAsync(
                    message.Chat.Id,
                    threadId,
                    topic.Name,
                    ctx.CancellationToken);
            }

            var envelope = MediaIndexService.FromTelegramMessage(message);
            if (envelope is not null)
                await mediaIndexService.IndexAsync(envelope, ctx.CancellationToken);
        }

        await next(ctx);
    }

    private static bool IsUserContentMessage(MessageType type)
        => type is MessageType.Text
            or MessageType.Photo
            or MessageType.Audio
            or MessageType.Video
            or MessageType.Voice
            or MessageType.Document
            or MessageType.Sticker
            or MessageType.Location
            or MessageType.Contact
            or MessageType.Venue
            or MessageType.Game
            or MessageType.VideoNote
            or MessageType.Invoice
            or MessageType.Poll
            or MessageType.Dice
            or MessageType.WebAppData
            or MessageType.Animation
            or MessageType.Story
            or MessageType.PassportData
            or MessageType.PaidMedia
            or MessageType.Checklist;
}
