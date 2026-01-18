using System.Text;
using System.Collections.Generic;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramBotKit.Keyboards;

/// <summary>
/// Provides a keyboard.
/// </summary>
public static partial class Keyboard
{
    private const int MaxCallbackDataBytes = 64;

    /// <summary>
    /// Creates the callback button.
    /// </summary>
    public static InlineKeyboardButton Callback(string text, string key, params string[] args)
        => InlineKeyboardButton.WithCallbackData(text, PackCallbackData(key, args));

    /// <summary>
    /// Creates the callback raw button.
    /// </summary>
    public static InlineKeyboardButton CallbackRaw(string text, string data)
        => InlineKeyboardButton.WithCallbackData(text, ValidateCallbackData(data));

    /// <summary>
    /// Creates the url button.
    /// </summary>
    public static InlineKeyboardButton Url(string text, string url)
        => InlineKeyboardButton.WithUrl(text, url);

    /// <summary>
    /// Creates the switch inline button.
    /// </summary>
    public static InlineKeyboardButton SwitchInline(string text, string query)
        => InlineKeyboardButton.WithSwitchInlineQueryCurrentChat(text, query);

    /// <summary>
    /// Creates the inline button.
    /// </summary>
    public static InlineKeyboardMarkup Inline(IEnumerable<IEnumerable<InlineKeyboardButton>> rows)
    {
        if (rows is null) throw new ArgumentNullException(nameof(rows));

        // Materialize without LINQ to reduce allocations.
        var list = new List<InlineKeyboardButton[]>();

        foreach (var row in rows)
        {
            if (row is null)
                continue;

            // Fast path: already an array.
            if (row is InlineKeyboardButton[] arr)
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

                var filtered = new InlineKeyboardButton[count];
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

            // General path: enumerate.
            List<InlineKeyboardButton>? tmp = null;
            foreach (var b in row)
            {
                if (b is null) continue;
                tmp ??= new List<InlineKeyboardButton>();
                tmp.Add(b);
            }

            if (tmp is { Count: > 0 })
                list.Add(tmp.ToArray());
        }

        return new InlineKeyboardMarkup(list.ToArray());
    }

    /// <summary>
    /// Creates the row.
    /// </summary>
    public static IReadOnlyList<InlineKeyboardButton> Row(params InlineKeyboardButton[] buttons)
        => buttons ?? Array.Empty<InlineKeyboardButton>();

    private static string PackCallbackData(string key, params string[] args)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new TelegramBotKitCallbackDataException("Callback key is required.");

        if (key.Contains(' '))
            throw new TelegramBotKitCallbackDataException("Callback key must not contain spaces.");

        if (args is null || args.Length == 0)
            return ValidateCallbackData(key);

        var sb = new StringBuilder(key);

        foreach (var a in args)
        {
            if (string.IsNullOrWhiteSpace(a))
                continue;

            if (a.Contains(' '))
                throw new TelegramBotKitCallbackDataException("Callback args must not contain spaces.");

            sb.Append(' ').Append(a);
        }

        return ValidateCallbackData(sb.ToString());
    }

    private static string ValidateCallbackData(string data)
    {
        if (string.IsNullOrWhiteSpace(data))
            throw new TelegramBotKitCallbackDataException("callback_data must not be empty.");

        var bytes = Encoding.UTF8.GetByteCount(data);
        if (bytes > MaxCallbackDataBytes)
            throw new TelegramBotKitCallbackDataException(
                $"callback_data is too long: {bytes} bytes (max {MaxCallbackDataBytes}). Reduce key/args.");

        return data;
    }
}
