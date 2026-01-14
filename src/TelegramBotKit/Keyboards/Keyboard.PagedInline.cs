using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramBotKit.Keyboards;

public static partial class Keyboard
{
    private const int DefaultMaxRowsPerPage = 8; // sensible default for UX
    private const int HardMaxRowsPerPage = 15;   // safety guard, avoid huge markups

    /// <summary>
    /// Creates a paged inline keyboard from existing rows.
    /// </summary>
    public static InlineKeyboardMarkup PagedInline(
        IEnumerable<IEnumerable<InlineKeyboardButton>> rows,
        string? navKey,
        int page,
        int pageRows = 8,
        string navArgsPrefix = "p",
        bool hideUselessArrows = true)
    {
        if (rows is null) throw new ArgumentNullException(nameof(rows));
        if (string.IsNullOrWhiteSpace(navArgsPrefix))
            throw new ArgumentException("Navigation prefix is required.", nameof(navArgsPrefix));

        // If navKey is not provided, we still can paginate the visible rows,
        // but we cannot create navigation buttons.
        var canNavigate = !string.IsNullOrWhiteSpace(navKey);

        pr = Math.Clamp(pr, 1, HardMaxRowsPerPage);

        // Normalize rows (same style as Inline()).
        var materialized = rows
            .Select(r => (r ?? Array.Empty<InlineKeyboardButton>())
                .Where(b => b is not null)
                .ToArray())
            .Where(r => r.Length > 0)
            .ToArray();

        // If everything fits into one page, return as-is (no nav row).
        if (materialized.Length <= pr || !canNavigate)
            return new InlineKeyboardMarkup(materialized);

        var totalPages = (int)Math.Ceiling(materialized.Length / (double)pr);
        page = Math.Clamp(page, 0, totalPages - 1);

        var start = page * pr;
        var pageSlice = materialized
            .Skip(start)
            .Take(pr)
            .Select(r => (IReadOnlyList<InlineKeyboardButton>)r)
            .ToList();

        // Add nav row only when it is actually needed (more than one page).
        pageSlice.Add(BuildNavRow(navKey!, navArgsPrefix, page, totalPages, hideUselessArrows));

        return new InlineKeyboardMarkup(pageSlice);
    }

    private static IReadOnlyList<InlineKeyboardButton> BuildNavRow(
        string key,
        string prefix,
        int page,
        int totalPages,
        bool hideUselessArrows)
    {
        // Middle label (always present)
        var mid = CallbackRaw($"{page + 1}/{totalPages}", "noop");

        // Left arrow
        InlineKeyboardButton? left = null;
        if (page > 0)
            left = Callback("⬅️", key, prefix, (page - 1).ToString());
        else if (!hideUselessArrows)
            left = CallbackRaw("⬅️", "noop");

        // Right arrow
        InlineKeyboardButton? right = null;
        if (page < totalPages - 1)
            right = Callback("➡️", key, prefix, (page + 1).ToString());
        else if (!hideUselessArrows)
            right = CallbackRaw("➡️", "noop");

        // Build row depending on which arrows are present.
        if (left is null && right is null)
            return Row(mid);

        if (left is null)
            return Row(mid, right!);          // [1/3] [➡️]

        if (right is null)
            return Row(left, mid);           // [⬅️] [1/3]

        return Row(left, mid, right);        // [⬅️] [1/3] [➡️]
    }
}
