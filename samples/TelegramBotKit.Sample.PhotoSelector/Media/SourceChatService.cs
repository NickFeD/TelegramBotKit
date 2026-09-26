using Microsoft.EntityFrameworkCore;
using TelegramBotKit.Sample.PhotoSelector.Access;
using TelegramBotKit.Sample.PhotoSelector.Data;
using TelegramBotKit.Sample.PhotoSelector.Data.Entities;

namespace TelegramBotKit.Sample.PhotoSelector.Media;

public sealed class SourceChatService(
    IDbContextFactory<PhotoSelectorDbContext> contextFactory,
    AccessService accessService)
{
    public async Task SetEnabledAsync(
        long administratorId,
        long chatId,
        string title,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        if (!await accessService.IsAdminAsync(administratorId, cancellationToken))
            throw new UnauthorizedAccessException("Только администратор может управлять источниками.");

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var source = await db.SourceChats.SingleOrDefaultAsync(x => x.ChatId == chatId, cancellationToken);
        if (source is null)
        {
            source = new SourceChat
            {
                ChatId = chatId,
                Title = title,
                IsEnabled = enabled,
                AddedByUserId = administratorId,
                CreatedAtUtc = DateTime.UtcNow
            };
            db.SourceChats.Add(source);
        }
        else
        {
            source.Title = title;
            source.IsEnabled = enabled;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
