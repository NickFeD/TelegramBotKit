using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TelegramBotKit.Sample.PhotoSelector.Data;
using TelegramBotKit.Sample.PhotoSelector.Data.Entities;

namespace TelegramBotKit.Sample.PhotoSelector.Browsing;

public sealed record BrowsePage(
    MediaItem Media,
    string SourceTitle,
    string TopicTitle,
    int Position,
    int Total,
    int SelectedCount);

public sealed class BrowseService(IDbContextFactory<PhotoSelectorDbContext> contextFactory)
{
    public async Task<BrowsePage?> StartAsync(
        long userId,
        BrowseFilter filter,
        long? sourceChatId = null,
        int? sourceThreadId = null,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.OpenConnectionAsync(cancellationToken);
        var connection = (SqliteConnection)db.Database.GetDbConnection();
        await using var transaction = connection.BeginTransaction(deferred: false);
        await db.Database.UseTransactionAsync(transaction, cancellationToken);

        var firstId = await BuildFilteredQuery(db, userId, filter, sourceChatId, sourceThreadId)
            .OrderBy(x => x.Id)
            .Select(x => (long?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var session = await db.BrowseSessions.FindAsync([userId], cancellationToken);
        if (session is null)
        {
            session = new BrowseSession { UserId = userId };
            db.BrowseSessions.Add(session);
        }

        session.Filter = filter;
        session.SourceChatId = sourceChatId;
        session.SourceThreadId = sourceThreadId;
        session.CurrentMediaItemId = firstId;
        session.BrowseMessageId = null;
        session.HistoryMediaItemIds = firstId?.ToString() ?? string.Empty;
        session.HistoryIndex = firstId.HasValue ? 0 : -1;
        session.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        var page = firstId.HasValue
            ? await CreatePageAsync(db, session, firstId.Value, cancellationToken)
            : null;
        await transaction.CommitAsync(cancellationToken);
        return page;
    }

    public async Task<BrowsePage?> GetCurrentAsync(long userId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var session = await db.BrowseSessions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        return session?.CurrentMediaItemId is { } mediaId
            ? await CreatePageAsync(db, session, mediaId, cancellationToken)
            : null;
    }

    public async Task<BrowsePage?> MoveAsync(
        long userId,
        long expectedMediaItemId,
        bool forward,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.OpenConnectionAsync(cancellationToken);
        var connection = (SqliteConnection)db.Database.GetDbConnection();
        await using var transaction = connection.BeginTransaction(deferred: false);
        await db.Database.UseTransactionAsync(transaction, cancellationToken);

        var session = await db.BrowseSessions.SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (session?.CurrentMediaItemId != expectedMediaItemId)
            return null;

        var history = ParseHistory(session.HistoryMediaItemIds);
        long? targetId;
        if (!forward)
        {
            if (session.HistoryIndex <= 0)
                targetId = session.CurrentMediaItemId;
            else
                targetId = history[--session.HistoryIndex];
        }
        else if (session.HistoryIndex + 1 < history.Count)
        {
            targetId = history[++session.HistoryIndex];
        }
        else
        {
            targetId = await BuildFilteredQuery(
                    db,
                    userId,
                    session.Filter,
                    session.SourceChatId,
                    session.SourceThreadId)
                .Where(x => x.Id > expectedMediaItemId)
                .OrderBy(x => x.Id)
                .Select(x => (long?)x.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (targetId.HasValue)
            {
                history.Add(targetId.Value);
                session.HistoryIndex++;
                session.HistoryMediaItemIds = string.Join(',', history);
            }
        }

        // Keep the last displayed item as the callback owner when forward navigation
        // reaches the end, so the user can still go back and change a decision.
        session.CurrentMediaItemId = targetId ?? expectedMediaItemId;
        session.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        var page = targetId.HasValue
            ? await CreatePageAsync(db, session, targetId.Value, cancellationToken)
            : null;
        await transaction.CommitAsync(cancellationToken);
        return page;
    }

    public async Task<bool> OwnsCallbackAsync(
        long userId,
        int callbackMessageId,
        long expectedMediaItemId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.BrowseSessions.AnyAsync(
            x => x.UserId == userId &&
                 x.BrowseMessageId == callbackMessageId &&
                 x.CurrentMediaItemId == expectedMediaItemId,
            cancellationToken);
    }

    public async Task SetBrowseMessageIdAsync(
        long userId,
        int messageId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var session = await db.BrowseSessions.FindAsync([userId], cancellationToken);
        if (session is null)
            return;
        session.BrowseMessageId = messageId;
        session.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static IQueryable<MediaItem> BuildFilteredQuery(
        PhotoSelectorDbContext db,
        long userId,
        BrowseFilter filter,
        long? sourceChatId,
        int? sourceThreadId)
    {
        var query = db.MediaItems.AsNoTracking();
        if (sourceChatId.HasValue)
            query = query.Where(x => x.SourceChatId == sourceChatId.Value);
        if (sourceThreadId.HasValue)
            query = query.Where(x => x.SourceThreadId == sourceThreadId.Value);

        return filter switch
        {
            BrowseFilter.Unreviewed => query.Where(x => !x.Selections.Any(s => s.UserId == userId)),
            BrowseFilter.Liked => query.Where(x => x.Selections.Any(
                s => s.UserId == userId && s.State == SelectionState.Liked)),
            BrowseFilter.Skipped => query.Where(x => x.Selections.Any(
                s => s.UserId == userId && s.State == SelectionState.Skipped)),
            _ => query
        };
    }

    private static async Task<BrowsePage?> CreatePageAsync(
        PhotoSelectorDbContext db,
        BrowseSession session,
        long mediaId,
        CancellationToken cancellationToken)
    {
        var media = await db.MediaItems.AsNoTracking()
            .Include(x => x.SourceChat)
            .SingleOrDefaultAsync(x => x.Id == mediaId, cancellationToken);
        if (media is null)
            return null;

        var topicTitle = "Общая лента";
        if (media.SourceThreadId is { } threadId)
        {
            topicTitle = await db.SourceTopics.AsNoTracking()
                .Where(x => x.ChatId == media.SourceChatId && x.ThreadId == threadId)
                .Select(x => x.Title)
                .SingleOrDefaultAsync(cancellationToken)
                ?? $"Тема №{threadId}";
        }

        var collection = BuildFilteredQuery(
            db,
            session.UserId,
            session.Filter,
            session.SourceChatId,
            session.SourceThreadId);
        var total = await collection.CountAsync(cancellationToken);
        var position = await collection.CountAsync(x => x.Id <= mediaId, cancellationToken);
        if (position == 0)
        {
            var history = ParseHistory(session.HistoryMediaItemIds);
            position = Math.Max(1, history.IndexOf(mediaId) + 1);
            total = Math.Max(total, history.Count);
        }

        var selected = await db.Selections.CountAsync(
            x => x.UserId == session.UserId && x.State == SelectionState.Liked,
            cancellationToken);

        return new BrowsePage(media, media.SourceChat.Title, topicTitle, position, total, selected);
    }

    private static List<long> ParseHistory(string value)
        => value.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(long.Parse)
            .ToList();
}
