namespace TelegramBotKit.Sample.PhotoSelector.Data.Entities;

public sealed class BotUser
{
    public long TelegramUserId { get; set; }
    public UserRole Role { get; set; }
    public bool IsEnabled { get; set; }
    public long? GrantedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public ICollection<Selection> Selections { get; set; } = [];
}

public enum UserRole
{
    User = 0,
    Admin = 1
}
