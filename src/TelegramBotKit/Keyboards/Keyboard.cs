using System.Text;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramBotKit.Keyboards;

public static class Keyboard
{
    private const int MaxCallbackDataBytes = 64;

    public static InlineKeyboardButton Callback(string text, string key, params string[] args)
        => InlineKeyboardButton.WithCallbackData(text, PackCallbackData(key, args));

    public static InlineKeyboardButton CallbackRaw(string text, string data)
        => InlineKeyboardButton.WithCallbackData(text, ValidateCallbackData(data));

    public static InlineKeyboardButton Url(string text, string url)
        => InlineKeyboardButton.WithUrl(text, url);

    public static InlineKeyboardButton SwitchInline(string text, string query)
        => InlineKeyboardButton.WithSwitchInlineQueryCurrentChat(text, query);

    public static InlineKeyboardMarkup Inline(IEnumerable<IEnumerable<InlineKeyboardButton>> rows)
    {
        if (rows is null) throw new ArgumentNullException(nameof(rows));

        var materialized = rows
            .Select(r => (r ?? Array.Empty<InlineKeyboardButton>())
                .Where(b => b is not null)
                .ToArray())
            .Where(r => r.Length > 0)
            .ToArray();

        return new InlineKeyboardMarkup(materialized);
    }

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
