namespace TelegramBotKit.Sample.PhotoSelector.Data.Entities;

public sealed class Selection
{
    public long UserId { get; set; }
    public long MediaItemId { get; set; }
    public SelectionState State { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public BotUser User { get; set; } = null!;
    public MediaItem MediaItem { get; set; } = null!;
}

public enum SelectionState
{
    Liked = 0,
    Skipped = 1
}
