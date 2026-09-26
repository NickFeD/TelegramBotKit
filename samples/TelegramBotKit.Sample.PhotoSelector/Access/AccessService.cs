using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TelegramBotKit.Sample.PhotoSelector.Data;
using TelegramBotKit.Sample.PhotoSelector.Data.Entities;

namespace TelegramBotKit.Sample.PhotoSelector.Access;

public sealed class AccessService(IDbContextFactory<PhotoSelectorDbContext> contextFactory)
{
    public async Task<bool> TryBootstrapAdminAsync(
        long userId,
        bool isPrivateChat,
        bool isHuman,
        bool isIncomingUserMessage,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0 || !isPrivateChat || !isHuman || !isIncomingUserMessage)
            return false;

        return await ExecuteImmediateAsync(async db =>
        {
            if (await db.Users.AnyAsync(x => x.Role == UserRole.Admin, cancellationToken))
                return false;

            db.Users.Add(new BotUser
            {
                TelegramUserId = userId,
                Role = UserRole.Admin,
                IsEnabled = true,
                CreatedAtUtc = DateTime.UtcNow
            });

            await db.SaveChangesAsync(cancellationToken);
            return true;
        }, cancellationToken);
    }

    public async Task<BotUser?> FindEnabledAsync(long userId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Users.AsNoTracking()
            .SingleOrDefaultAsync(x => x.TelegramUserId == userId && x.IsEnabled, cancellationToken);
    }

    public async Task<bool> IsAuthorizedAsync(long userId, CancellationToken cancellationToken = default)
        => await FindEnabledAsync(userId, cancellationToken) is not null;

    public async Task<bool> IsAdminAsync(long userId, CancellationToken cancellationToken = default)
        => (await FindEnabledAsync(userId, cancellationToken))?.Role == UserRole.Admin;

    public async Task GrantAsync(long administratorId, long userId, CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
            throw new ArgumentOutOfRangeException(nameof(userId));

        await ExecuteImmediateAsync(async db =>
        {
            if (!await IsEnabledAdminAsync(db, administratorId, cancellationToken))
                throw new UnauthorizedAccessException("Только администратор может выдавать доступ.");

            var user = await db.Users.SingleOrDefaultAsync(x => x.TelegramUserId == userId, cancellationToken);
            if (user is null)
            {
                db.Users.Add(new BotUser
                {
                    TelegramUserId = userId,
                    Role = UserRole.User,
                    IsEnabled = true,
                    GrantedByUserId = administratorId,
                    CreatedAtUtc = DateTime.UtcNow
                });
            }
            else if (user.Role != UserRole.Admin)
            {
                user.IsEnabled = true;
                user.GrantedByUserId = administratorId;
            }

            await db.SaveChangesAsync(cancellationToken);
            return true;
        }, cancellationToken);
    }

    public async Task RevokeAsync(long administratorId, long userId, CancellationToken cancellationToken = default)
    {
        if (administratorId == userId)
            throw new InvalidOperationException("Администратор не может отозвать собственный доступ.");

        await ExecuteImmediateAsync(async db =>
        {
            if (!await IsEnabledAdminAsync(db, administratorId, cancellationToken))
                throw new UnauthorizedAccessException("Только администратор может отзывать доступ.");

            var user = await db.Users.SingleOrDefaultAsync(x => x.TelegramUserId == userId, cancellationToken);
            if (user is not null && user.Role != UserRole.Admin)
                user.IsEnabled = false;

            await db.SaveChangesAsync(cancellationToken);
            return true;
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<BotUser>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Users.AsNoTracking()
            .OrderByDescending(x => x.Role)
            .ThenBy(x => x.TelegramUserId)
            .ToListAsync(cancellationToken);
    }

    private static Task<bool> IsEnabledAdminAsync(
        PhotoSelectorDbContext db,
        long userId,
        CancellationToken cancellationToken)
        => db.Users.AnyAsync(
            x => x.TelegramUserId == userId && x.IsEnabled && x.Role == UserRole.Admin,
            cancellationToken);

    private async Task<T> ExecuteImmediateAsync<T>(
        Func<PhotoSelectorDbContext, Task<T>> action,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
            await db.Database.OpenConnectionAsync(cancellationToken);

            try
            {
                var connection = (SqliteConnection)db.Database.GetDbConnection();
                await using var transaction = connection.BeginTransaction(deferred: false);
                await db.Database.UseTransactionAsync(transaction, cancellationToken);
                var result = await action(db);
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch (SqliteException exception) when (exception.SqliteErrorCode is 5 or 6 && attempt < 5)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(25 * attempt), cancellationToken);
            }
        }
    }
}
