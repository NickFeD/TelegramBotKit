using Microsoft.EntityFrameworkCore;
using TelegramBotKit.Sample.PhotoSelector.Data;
using TelegramBotKit.Sample.PhotoSelector.Data.Entities;

namespace TelegramBotKit.Sample.PhotoSelector.Media;

public sealed record ExportItem(long MediaItemId, MediaKind MediaKind, string FileId, string? FileName);

public sealed class ExportService(IDbContextFactory<PhotoSelectorDbContext> contextFactory)
{
    public async Task<IReadOnlyList<ExportItem>> GetLikedAsync(
        long userId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Selections.AsNoTracking()
            .Where(x => x.UserId == userId && x.State == SelectionState.Liked)
            .OrderBy(x => x.MediaItemId)
            .Select(x => new ExportItem(
                x.MediaItemId,
                x.MediaItem.MediaKind,
                x.MediaItem.OriginalFileId,
                x.MediaItem.FileName))
            .ToListAsync(cancellationToken);
    }
}
