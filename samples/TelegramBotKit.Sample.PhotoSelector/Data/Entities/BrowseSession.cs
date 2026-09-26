namespace TelegramBotKit.Sample.PhotoSelector.Data.Entities;

public sealed class BrowseSession
{
    public long UserId { get; set; }
    public BrowseFilter Filter { get; set; }
    public long? SourceChatId { get; set; }
    public int? SourceThreadId { get; set; }
    public long? CurrentMediaItemId { get; set; }
    public int? BrowseMessageId { get; set; }
    public string HistoryMediaItemIds { get; set; } = string.Empty;
    public int HistoryIndex { get; set; } = -1;
    public DateTime UpdatedAtUtc { get; set; }
}

public enum BrowseFilter
{
    Unreviewed = 0,
    Liked = 1,
    Skipped = 2,
    All = 3
}
