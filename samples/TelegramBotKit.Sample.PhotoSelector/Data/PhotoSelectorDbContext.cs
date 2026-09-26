using Microsoft.EntityFrameworkCore;
using TelegramBotKit.Sample.PhotoSelector.Data.Entities;

namespace TelegramBotKit.Sample.PhotoSelector.Data;

public sealed class PhotoSelectorDbContext(DbContextOptions<PhotoSelectorDbContext> options)
    : DbContext(options)
{
    public DbSet<BotUser> Users => Set<BotUser>();
    public DbSet<SourceChat> SourceChats => Set<SourceChat>();
    public DbSet<SourceTopic> SourceTopics => Set<SourceTopic>();
    public DbSet<MediaItem> MediaItems => Set<MediaItem>();
    public DbSet<Selection> Selections => Set<Selection>();
    public DbSet<BrowseSession> BrowseSessions => Set<BrowseSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BotUser>(entity =>
        {
            entity.HasKey(x => x.TelegramUserId);
            entity.Property(x => x.TelegramUserId).ValueGeneratedNever();
            entity.Property(x => x.Role).HasConversion<string>().HasMaxLength(16);
            entity.HasIndex(x => new { x.Role, x.IsEnabled });
        });

        modelBuilder.Entity<SourceChat>(entity =>
        {
            entity.HasKey(x => x.ChatId);
            entity.Property(x => x.ChatId).ValueGeneratedNever();
            entity.Property(x => x.Title).HasMaxLength(255);
            entity.HasIndex(x => x.IsEnabled);
        });

        modelBuilder.Entity<SourceTopic>(entity =>
        {
            entity.HasKey(x => new { x.ChatId, x.ThreadId });
            entity.Property(x => x.Title).HasMaxLength(255);
            entity.HasOne(x => x.SourceChat)
                .WithMany(x => x.Topics)
                .HasForeignKey(x => x.ChatId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MediaItem>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.MediaKind).HasConversion<string>().HasMaxLength(24);
            entity.Property(x => x.OriginalFileId).HasMaxLength(512);
            entity.Property(x => x.OriginalFileUniqueId).HasMaxLength(255);
            entity.Property(x => x.PreviewFileId).HasMaxLength(512);
            entity.Property(x => x.PreviewFileUniqueId).HasMaxLength(255);
            entity.Property(x => x.FileName).HasMaxLength(255);
            entity.Property(x => x.MimeType).HasMaxLength(127);
            entity.HasIndex(x => new { x.SourceChatId, x.SourceMessageId }).IsUnique();
            entity.HasIndex(x => new { x.SourceChatId, x.SourceThreadId });
            entity.HasOne(x => x.SourceChat)
                .WithMany(x => x.MediaItems)
                .HasForeignKey(x => x.SourceChatId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Selection>(entity =>
        {
            entity.HasKey(x => new { x.UserId, x.MediaItemId });
            entity.Property(x => x.State).HasConversion<string>().HasMaxLength(16);
            entity.HasIndex(x => new { x.UserId, x.State });
            entity.HasOne(x => x.User)
                .WithMany(x => x.Selections)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.MediaItem)
                .WithMany(x => x.Selections)
                .HasForeignKey(x => x.MediaItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BrowseSession>(entity =>
        {
            entity.HasKey(x => x.UserId);
            entity.Property(x => x.UserId).ValueGeneratedNever();
            entity.Property(x => x.Filter).HasConversion<string>().HasMaxLength(16);
            entity.Property(x => x.HistoryMediaItemIds).HasMaxLength(4000);
        });
    }
}
