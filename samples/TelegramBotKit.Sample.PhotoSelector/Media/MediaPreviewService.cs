using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramBotKit.Sample.PhotoSelector.Data;
using TelegramBotKit.Sample.PhotoSelector.Data.Entities;

namespace TelegramBotKit.Sample.PhotoSelector.Media;

public sealed class MediaPreviewService(IDbContextFactory<PhotoSelectorDbContext> contextFactory)
{
    private const long TelegramDownloadLimit = 20 * 1024 * 1024;

    public async Task<MemoryStream?> DownloadOriginalAsync(
        ITelegramBotClient botClient,
        MediaItem media,
        CancellationToken cancellationToken)
    {
        if (media.FileSize is > TelegramDownloadLimit)
            return null;

        var file = await botClient.GetFile(media.OriginalFileId, cancellationToken);
        var stream = media.FileSize is > 0 and <= int.MaxValue
            ? new MemoryStream((int)media.FileSize.Value)
            : new MemoryStream();

        try
        {
            await botClient.DownloadFile(file, stream, cancellationToken);
            stream.Position = 0;
            return stream;
        }
        catch
        {
            await stream.DisposeAsync();
            throw;
        }
    }

    public async Task SaveReusablePreviewAsync(
        long mediaId,
        Message message,
        MediaKind kind,
        CancellationToken cancellationToken)
    {
        var (fileId, uniqueId) = kind switch
        {
            MediaKind.ImageDocument when message.Photo is { Length: > 0 } photos =>
                photos.OrderByDescending(x => (long)x.Width * x.Height)
                    .Select(x => (x.FileId, x.FileUniqueId))
                    .First(),
            MediaKind.VideoDocument when message.Video is { } video =>
                (video.FileId, video.FileUniqueId),
            _ => (null, null)
        };

        if (fileId is null || uniqueId is null)
            return;

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var media = await db.MediaItems.SingleOrDefaultAsync(x => x.Id == mediaId, cancellationToken);
        if (media is null)
            return;

        media.PreviewFileId = fileId;
        media.PreviewFileUniqueId = uniqueId;
        media.PreviewIsReusable = true;
        await db.SaveChangesAsync(cancellationToken);
    }
}
