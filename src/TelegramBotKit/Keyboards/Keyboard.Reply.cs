using System.Collections.Generic;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramBotKit.Keyboards;

public static partial class Keyboard
{
    /// <summary>
    /// Creates a reply keyboard button with text.
    /// </summary>
    /// <param name="text">The button label displayed to the user.</param>
    /// <returns>A reply button that sends its label as a message.</returns>
    public static KeyboardButton Text(string text)
        => new(text);

    /// <summary>
    /// Creates a reply button that asks the user to share their contact.
    /// </summary>
    /// <param name="text">The button label displayed to the user.</param>
    /// <returns>A reply button that requests the user's contact.</returns>
    public static KeyboardButton RequestContact(string text)
        => KeyboardButton.WithRequestContact(text);

    /// <summary>
    /// Creates a reply button that asks the user to share their location.
    /// </summary>
    /// <param name="text">The button label displayed to the user.</param>
    /// <returns>A reply button that requests the user's location.</returns>
    public static KeyboardButton RequestLocation(string text)
        => KeyboardButton.WithRequestLocation(text);

    /// <summary>
    /// Creates a reply keyboard button that requests a poll.
    /// </summary>
    /// <param name="text">The button label displayed to the user.</param>
    /// <param name="pollType">The requested poll type.</param>
    /// <returns>A reply button that requests a poll of the supplied type.</returns>
    public static KeyboardButton RequestPoll(string text, KeyboardButtonPollType pollType)
        => KeyboardButton.WithRequestPoll(text, pollType);

    /// <summary>
    /// Groups reply buttons into a row without copying or filtering the supplied array.
    /// </summary>
    /// <param name="buttons">The row's buttons, or null for an empty row.</param>
    /// <returns>The supplied button array as a row, or an empty row when the array is null.</returns>
    public static IReadOnlyList<KeyboardButton> ReplyRow(params KeyboardButton[] buttons)
        => buttons ?? Array.Empty<KeyboardButton>();

    /// <summary>
    /// Materializes a reply keyboard, omitting null buttons and empty rows.
    /// </summary>
    /// <exception cref="ArgumentException">No nonempty rows remain after filtering.</exception>
    /// <param name="rows">The keyboard rows; null rows, null buttons and empty rows are omitted.</param>
    /// <param name="resizeKeyboard">Whether clients should fit the keyboard height to its buttons.</param>
    /// <param name="oneTimeKeyboard">Whether clients should hide the keyboard after use.</param>
    /// <param name="selective">Whether Telegram should target only the selected users.</param>
    /// <param name="isPersistent">Whether clients should keep the reply keyboard available.</param>
    /// <param name="inputFieldPlaceholder">The input hint displayed while the keyboard is active, or null to omit it.</param>
    /// <returns>The filtered, nonempty reply keyboard with the supplied client options.</returns>
    public static ReplyKeyboardMarkup Reply(
        IEnumerable<IEnumerable<KeyboardButton>> rows,
        bool resizeKeyboard = true,
        bool oneTimeKeyboard = false,
        bool selective = false,
        bool isPersistent = false,
        string? inputFieldPlaceholder = null)
    {
        if (rows is null) throw new ArgumentNullException(nameof(rows));

        // Materialize without LINQ to reduce allocations.
        var list = new List<KeyboardButton[]>();

        foreach (var row in rows)
        {
            if (row is null)
                continue;

            if (row is KeyboardButton[] arr)
            {
                var count = 0;
                for (var i = 0; i < arr.Length; i++)
                {
                    if (arr[i] is not null)
                        count++;
                }

                if (count == 0)
                    continue;

                if (count == arr.Length)
                {
                    list.Add(arr);
                    continue;
                }

                var filtered = new KeyboardButton[count];
                var idx = 0;
                for (var i = 0; i < arr.Length; i++)
                {
                    var b = arr[i];
                    if (b is not null)
                        filtered[idx++] = b;
                }

                list.Add(filtered);
                continue;
            }

            List<KeyboardButton>? tmp = null;
            foreach (var b in row)
            {
                if (b is null) continue;
                tmp ??= new List<KeyboardButton>();
                tmp.Add(b);
            }

            if (tmp is { Count: > 0 })
                list.Add(tmp.ToArray());
        }

        if (list.Count == 0)
            throw new ArgumentException("Reply keyboard must contain at least one non-empty row.", nameof(rows));

        return new ReplyKeyboardMarkup(list.ToArray())
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
    /// <param name="selective">Whether Telegram should target only the selected users.</param>
    /// <returns>Markup instructing clients to remove the reply keyboard.</returns>
    public static ReplyKeyboardRemove RemoveReplyKeyboard(bool selective = false)
        => new() { Selective = selective };

}
