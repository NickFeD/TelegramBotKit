namespace TelegramBotKit.Sample.PhotoSelector.Data.Entities;

public sealed class SourceChat
{
    public long ChatId { get; set; }
    public required string Title { get; set; }
    public bool IsEnabled { get; set; }
    public long AddedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public ICollection<SourceTopic> Topics { get; set; } = [];
    public ICollection<MediaItem> MediaItems { get; set; } = [];
}
