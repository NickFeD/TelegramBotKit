using TelegramBotKit.Sample.PhotoSelector.Data.Entities;

namespace TelegramBotKit.Sample.PhotoSelector.Media;

public sealed record MediaEnvelope(
    long SourceChatId,
    string SourceChatTitle,
    int SourceMessageId,
    int? SourceThreadId,
    long? SourceSenderUserId,
    MediaKind MediaKind,
    string OriginalFileId,
    string OriginalFileUniqueId,
    string? PreviewFileId,
    string? PreviewFileUniqueId,
    bool PreviewIsReusable,
    string? FileName,
    string? MimeType,
    long? FileSize,
    int? Width,
    int? Height,
    DateTime TelegramMessageDateUtc,
    string? TopicTitle);
