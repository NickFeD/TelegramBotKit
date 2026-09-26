using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TelegramBotKit.Sample.PhotoSelector.Data;
using TelegramBotKit.Sample.PhotoSelector.Data.Entities;

namespace TelegramBotKit.Sample.PhotoSelector.Browsing;

public sealed class SelectionService(IDbContextFactory<PhotoSelectorDbContext> contextFactory)
{
    public async Task SetAsync(
        long userId,
        long mediaItemId,
        SelectionState state,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.OpenConnectionAsync(cancellationToken);
        var connection = (SqliteConnection)db.Database.GetDbConnection();
        await using var transaction = connection.BeginTransaction(deferred: false);
        await db.Database.UseTransactionAsync(transaction, cancellationToken);

        var selection = await db.Selections.FindAsync([userId, mediaItemId], cancellationToken);
        if (selection is null)
        {
            db.Selections.Add(new Selection
            {
                UserId = userId,
                MediaItemId = mediaItemId,
                State = state,
                UpdatedAtUtc = DateTime.UtcNow
            });
        }
        else
        {
            selection.State = state;
            selection.UpdatedAtUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
