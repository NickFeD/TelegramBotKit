using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramBotKit.Keyboards;

public static partial class Keyboard
{
    private const int DefaultMaxRowsPerPage = 8;
    private const int HardMaxRowsPerPage = 15;

    /// <summary>
    /// Builds a page of inline rows with callback navigation, or returns all rows when navigation is disabled.
    /// </summary>
    /// <remarks>
    /// Navigation data contains the navigation key, argument prefix and zero-based destination page.
    /// The page label and disabled arrows emit <c>noop</c>; this helper does not register a handler for it.
    /// </remarks>
    /// <param name="rows">The keyboard rows; null rows, null buttons and empty rows are omitted.</param>
    /// <param name="navKey">The callback key for navigation. A blank key returns all rows without navigation.</param>
    /// <param name="page">The zero-based page index, clamped to the available pages when navigation is enabled.</param>
    /// <param name="pageRows">Rows per page; nonpositive values use eight and positive values are capped at fifteen.</param>
    /// <param name="navArgsPrefix">The first navigation argument, followed by the zero-based destination page.</param>
    /// <param name="hideUselessArrows">Whether arrows without a destination are hidden; otherwise they emit the raw noop callback.</param>
    /// <returns>The visible keyboard rows and, when needed, a navigation row.</returns>
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

        // A missing key disables both slicing and navigation.
        var canNavigate = !string.IsNullOrWhiteSpace(navKey);

        var pr = pageRows <= 0 ? DefaultMaxRowsPerPage : pageRows;
        pr = Math.Clamp(pr, 1, HardMaxRowsPerPage);

        // Remove empty rows before calculating page boundaries.
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
