namespace TelegramBotKit.Sample.PhotoSelector.Data.Entities;

public sealed class SourceTopic
{
    public long ChatId { get; set; }
    public int ThreadId { get; set; }
    public required string Title { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public SourceChat SourceChat { get; set; } = null!;
}
