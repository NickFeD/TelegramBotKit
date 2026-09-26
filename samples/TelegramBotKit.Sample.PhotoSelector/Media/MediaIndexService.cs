using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot.Types;
using TelegramBotKit.Sample.PhotoSelector.Data;
using TelegramBotKit.Sample.PhotoSelector.Data.Entities;

namespace TelegramBotKit.Sample.PhotoSelector.Media;

public sealed class MediaIndexService(IDbContextFactory<PhotoSelectorDbContext> contextFactory)
{
    private static readonly HashSet<string> SupportedImageMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    private static readonly HashSet<string> SupportedVideoMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "video/mp4"
    };

    public static MediaEnvelope? FromTelegramMessage(Message message)
    {
        if (message.Photo is { Length: > 0 })
        {
            var best = message.Photo
                .OrderByDescending(x => (long)x.Width * x.Height)
                .ThenByDescending(x => x.FileSize ?? 0)
                .First();

            return Create(
                message,
                MediaKind.Photo,
                best.FileId,
                best.FileUniqueId,
                best.FileId,
                best.FileUniqueId,
                true,
                null,
                "image/jpeg",
                best.FileSize,
                best.Width,
                best.Height);
        }

        if (message.Video is { } video)
        {
            return Create(
                message,
                MediaKind.Video,
                video.FileId,
                video.FileUniqueId,
                video.FileId,
                video.FileUniqueId,
                true,
                video.FileName,
                video.MimeType ?? "video/mp4",
                video.FileSize,
                video.Width,
                video.Height);
        }

        var document = message.Document;
        if (document is null || document.MimeType is null)
            return null;

        var kind = SupportedImageMimeTypes.Contains(document.MimeType)
            ? MediaKind.ImageDocument
            : SupportedVideoMimeTypes.Contains(document.MimeType)
                ? MediaKind.VideoDocument
                : (MediaKind?)null;
        if (kind is null)
            return null;

        return Create(
            message,
            kind.Value,
            document.FileId,
            document.FileUniqueId,
            document.Thumbnail?.FileId,
            document.Thumbnail?.FileUniqueId,
            false,
            document.FileName,
            document.MimeType,
            document.FileSize,
            document.Thumbnail?.Width,
            document.Thumbnail?.Height);
    }

    public async Task<bool> IndexAsync(MediaEnvelope envelope, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        if (!await db.SourceChats.AnyAsync(
                x => x.ChatId == envelope.SourceChatId && x.IsEnabled,
                cancellationToken))
            return false;

        if (await db.MediaItems.AnyAsync(
                x => x.SourceChatId == envelope.SourceChatId &&
                     x.SourceMessageId == envelope.SourceMessageId,
                cancellationToken))
            return false;

        if (envelope.SourceThreadId is { } threadId)
            await UpsertTopicAsync(db, envelope.SourceChatId, threadId, envelope.TopicTitle, cancellationToken);

        db.MediaItems.Add(new MediaItem
        {
            SourceChatId = envelope.SourceChatId,
            SourceMessageId = envelope.SourceMessageId,
            SourceThreadId = envelope.SourceThreadId,
            SourceSenderUserId = envelope.SourceSenderUserId,
            MediaKind = envelope.MediaKind,
            OriginalFileId = envelope.OriginalFileId,
            OriginalFileUniqueId = envelope.OriginalFileUniqueId,
            PreviewFileId = envelope.PreviewFileId,
            PreviewFileUniqueId = envelope.PreviewFileUniqueId,
            PreviewIsReusable = envelope.PreviewIsReusable,
            FileName = envelope.FileName,
            MimeType = envelope.MimeType,
            FileSize = envelope.FileSize,
            Width = envelope.Width,
            Height = envelope.Height,
            TelegramMessageDateUtc = envelope.TelegramMessageDateUtc.ToUniversalTime(),
            IndexedAtUtc = DateTime.UtcNow
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is SqliteException { SqliteErrorCode: 19 })
        {
            return false;
        }
    }

    public async Task ObserveTopicAsync(
        long chatId,
        int threadId,
        string title,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        if (!await db.SourceChats.AnyAsync(x => x.ChatId == chatId && x.IsEnabled, cancellationToken))
            return;

        await UpsertTopicAsync(db, chatId, threadId, title, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static MediaEnvelope Create(
        Message message,
        MediaKind kind,
        string originalFileId,
        string originalFileUniqueId,
        string? previewFileId,
        string? previewFileUniqueId,
        bool previewIsReusable,
        string? fileName,
        string? mimeType,
        long? fileSize,
        int? width,
        int? height)
        => new(
            message.Chat.Id,
            message.Chat.Title ?? message.Chat.Username ?? message.Chat.Id.ToString(),
            message.Id,
            message.MessageThreadId,
            message.From?.Id,
            kind,
            originalFileId,
            originalFileUniqueId,
            previewFileId,
            previewFileUniqueId,
            previewIsReusable,
            fileName,
            mimeType,
            fileSize,
            width,
            height,
            message.Date.ToUniversalTime(),
            message.ForumTopicCreated?.Name);

    private static async Task UpsertTopicAsync(
        PhotoSelectorDbContext db,
        long chatId,
        int threadId,
        string? title,
        CancellationToken cancellationToken)
    {
        var topic = await db.SourceTopics.FindAsync([chatId, threadId], cancellationToken);
        var resolvedTitle = string.IsNullOrWhiteSpace(title) ? $"Тема №{threadId}" : title;

        if (topic is null)
        {
            db.SourceTopics.Add(new SourceTopic
            {
                ChatId = chatId,
                ThreadId = threadId,
                Title = resolvedTitle,
                UpdatedAtUtc = DateTime.UtcNow
            });
        }
        else if (!string.IsNullOrWhiteSpace(title))
        {
            topic.Title = title;
            topic.UpdatedAtUtc = DateTime.UtcNow;
        }
    }
}
