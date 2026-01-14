using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramBotKit.Keyboards;

public static partial class Keyboard
{
    /// <summary>
    /// Creates a reply keyboard button with text.
    /// </summary>
    public static KeyboardButton Text(string text)
        => new(text);

    /// <summary>
    /// Creates a reply keyboard button that requests contact.
    /// </summary>
    public static KeyboardButton RequestContact(string text)
        => KeyboardButton.WithRequestContact(text);

    /// <summary>
    /// Creates a reply keyboard button that requests location.
    /// </summary>
    public static KeyboardButton RequestLocation(string text)
        => KeyboardButton.WithRequestLocation(text);

    /// <summary>
    /// Creates a reply keyboard button that requests a poll.
    /// </summary>
    public static KeyboardButton RequestPoll(string text, KeyboardButtonPollType? pollType = null)
        => KeyboardButton.WithRequestPoll(text, pollType);

    /// <summary>
    /// Creates a reply keyboard row.
    /// </summary>
    public static IReadOnlyList<KeyboardButton> ReplyRow(params KeyboardButton[] buttons)
        => buttons ?? Array.Empty<KeyboardButton>();

    /// <summary>
    /// Creates a reply keyboard markup.
    /// </summary>
    public static ReplyKeyboardMarkup Reply(
        IEnumerable<IEnumerable<KeyboardButton>> rows,
        bool resizeKeyboard = true,
        bool oneTimeKeyboard = false,
        bool selective = false,
        bool isPersistent = false,
        string? inputFieldPlaceholder = null)
    {
        if (rows is null) throw new ArgumentNullException(nameof(rows));

        var materialized = rows
            .Select(r => (r ?? Array.Empty<KeyboardButton>())
                .Where(b => b is not null)
                .ToArray())
            .Where(r => r.Length > 0)
            .ToArray();

        if (materialized.Length == 0)
            throw new ArgumentException("Reply keyboard must contain at least one non-empty row.", nameof(rows));

        return new ReplyKeyboardMarkup(materialized)
        {
            ResizeKeyboard = resizeKeyboard,
            OneTimeKeyboard = oneTimeKeyboard,
            Selective = selective,
            IsPersistent = isPersistent,
            InputFieldPlaceholder = inputFieldPlaceholder
        };
    }

    /// <summary>
    /// Creates a markup that removes the current reply keyboard.
    /// </summary>
    public static ReplyKeyboardRemove RemoveReplyKeyboard(bool selective = false)
        => new() { Selective = selective };

}
