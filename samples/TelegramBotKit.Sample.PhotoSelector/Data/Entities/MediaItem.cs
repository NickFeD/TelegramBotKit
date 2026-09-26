namespace TelegramBotKit.Sample.PhotoSelector.Data.Entities;

public sealed class MediaItem
{
    public long Id { get; set; }
    public long SourceChatId { get; set; }
    public int SourceMessageId { get; set; }
    public int? SourceThreadId { get; set; }
    public long? SourceSenderUserId { get; set; }
    public MediaKind MediaKind { get; set; }
    public required string OriginalFileId { get; set; }
    public required string OriginalFileUniqueId { get; set; }
    public string? PreviewFileId { get; set; }
    public string? PreviewFileUniqueId { get; set; }
    public bool PreviewIsReusable { get; set; }
    public string? FileName { get; set; }
    public string? MimeType { get; set; }
    public long? FileSize { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public DateTime TelegramMessageDateUtc { get; set; }
    public DateTime IndexedAtUtc { get; set; }

    public SourceChat SourceChat { get; set; } = null!;
    public ICollection<Selection> Selections { get; set; } = [];
}

public enum MediaKind
{
    Photo = 0,
    ImageDocument = 1,
    Video = 2,
    VideoDocument = 3
}
