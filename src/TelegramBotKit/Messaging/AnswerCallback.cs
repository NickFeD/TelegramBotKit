namespace TelegramBotKit.Messaging;

/// <summary>
/// DTO для AnswerCallbackQuery.
/// </summary>
public sealed record AnswerCallback
{
    /// <summary>Текст всплывашки (null — без текста).</summary>
    public string? Text { get; init; }

    /// <summary>Показать alert вместо toast.</summary>
    public bool ShowAlert { get; init; } = false;

    /// <summary>URL для открытия (редко нужно).</summary>
    public string? Url { get; init; }
}
