using System.Collections.Generic;
using System.Text;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramBotKit.Commands;

namespace TelegramBotKit.Keyboards;

/// <summary>
/// Builds Telegram inline and reply keyboards and validates callback payloads.
/// </summary>
public static partial class Keyboard
{
    private const int MaxCallbackDataBytes = 64;

    /// <summary>
    /// Creates a callback button by packing a routing key and its arguments into callback data.
    /// </summary>
    /// <exception cref="TelegramBotKitCallbackDataException">The key or arguments are invalid, or the packed data exceeds 64 UTF-8 bytes.</exception>
    /// <param name="text">The button label displayed to the user.</param>
    /// <param name="key">The callback routing key. It must be nonempty and contain no spaces.</param>
    /// <param name="args">Space-separated callback arguments; blank entries are omitted and embedded spaces are rejected.</param>
    /// <returns>An inline button containing the validated callback key and arguments.</returns>
    public static InlineKeyboardButton Callback(string text, string key, params string[] args)
        => InlineKeyboardButton.WithCallbackData(text, PackCallbackData(key, args));

    /// <summary>
    /// Creates a callback button using the handler's compiler-generated callback key.
    /// </summary>
    /// <exception cref="InvalidOperationException">The handler has no generated callback-key mapping.</exception>
    /// <exception cref="TelegramBotKitCallbackDataException">The key or arguments are invalid, or the packed data exceeds 64 UTF-8 bytes.</exception>
    /// <param name="text">The button label displayed to the user.</param>
    /// <param name="args">Space-separated callback arguments; blank entries are omitted and embedded spaces are rejected.</param>
    /// <typeparam name="THandler">The callback handler with a generated key mapping.</typeparam>
    /// <returns>An inline button containing the validated callback key and arguments.</returns>
    public static InlineKeyboardButton Callback<THandler>(string text, params string[] args)
    where THandler : ICallbackCommand
    => Callback(text, CallbackCommandKeyResolver.GetKey<THandler>(), args);

    /// <summary>
    /// Creates a callback button from raw data, validating that it occupies at most 64 UTF-8 bytes.
    /// </summary>
    /// <exception cref="TelegramBotKitCallbackDataException">The data is blank or exceeds 64 UTF-8 bytes.</exception>
    /// <param name="text">The button label displayed to the user.</param>
    /// <param name="data">The nonempty raw callback data, limited to 64 UTF-8 bytes.</param>
    /// <returns>An inline button containing the validated raw callback data.</returns>
    public static InlineKeyboardButton CallbackRaw(string text, string data)
        => InlineKeyboardButton.WithCallbackData(text, ValidateCallbackData(data));

    /// <summary>
    /// Creates an inline button that opens a URL.
    /// </summary>
    /// <param name="text">The button label displayed to the user.</param>
    /// <param name="url">The URL opened when the button is pressed.</param>
    /// <returns>An inline button that opens the supplied URL.</returns>
    public static InlineKeyboardButton Url(string text, string url)
        => InlineKeyboardButton.WithUrl(text, url);

    /// <summary>
    /// Creates a button that starts an inline query in the current chat.
    /// </summary>
    /// <param name="text">The button label displayed to the user.</param>
    /// <param name="query">The initial inline query inserted in the current chat.</param>
    /// <returns>An inline button that inserts the supplied query in the current chat.</returns>
    public static InlineKeyboardButton SwitchInline(string text, string query)
        => InlineKeyboardButton.WithSwitchInlineQueryCurrentChat(text, query);

    /// <summary>
    /// Materializes an inline keyboard, omitting null buttons and empty rows.
    /// </summary>
    /// <param name="rows">The keyboard rows; null rows, null buttons and empty rows are omitted.</param>
    /// <returns>The materialized inline keyboard; it may contain no rows.</returns>
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
    /// Groups inline buttons into a row without copying or filtering the supplied array.
    /// </summary>
    /// <param name="buttons">The row's buttons, or null for an empty row.</param>
    /// <returns>The supplied button array as a row, or an empty row when the array is null.</returns>
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
