using Telegram.Bot.Types;
using TelegramBotKit.Commands;

namespace TelegramBotKit.Routing;

internal sealed class CommandRouter
{
    private readonly CommandRegistry _registry;

    public CommandRouter(CommandRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public async Task<bool> TryRouteMessageAsync(Message message, BotContext ctx)
    {
        ctx.CancellationToken.ThrowIfCancellationRequested();

        var text = message.Text;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        text = text.Trim();

        if (text.Length > 1 && text[0] == '/')
        {
            var cmd = ExtractSlashCommand(text);
            if (cmd is null)
                return false;

            // cmd is already normalized by ExtractSlashCommand (trimmed, leading '/', no '@bot').
            if (!_registry.TryGetMessageCommandNormalized(cmd, out var invoker))
                return false;

            await invoker(message, ctx).ConfigureAwait(false);
            return true;
        }

        // text is already trimmed above.
        if (!_registry.TryGetTextCommandNormalized(text, out var textInvoker))
            return false;

        await textInvoker(message, ctx).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> TryRouteCallbackAsync(CallbackQuery query, BotContext ctx)
    {
        ctx.CancellationToken.ThrowIfCancellationRequested();

        var data = query.Data;
        if (string.IsNullOrWhiteSpace(data))
            return false;

        if (!TryParseCallbackData(data, out var key, out var args))
            return false;

        // key is already trimmed by TryParseCallbackData.
        if (!_registry.TryGetCallbackCommandNormalized(key, out var invoker))
            return false;

        await invoker(query, args, ctx).ConfigureAwait(false);
        return true;
    }

    private static string? ExtractSlashCommand(string text)
    {
        // Hot path: avoid string.Split allocations.
        // Expected input is already trimmed by caller.
        if (text.Length < 2 || text[0] != '/')
            return null;

        // Find end of the first token (command), separated by spaces.
        var end = 0;
        while (end < text.Length && text[end] != ' ')
            end++;

        if (end < 2)
            return null;

        // Strip optional bot username: "/start@MyBot" -> "/start".
        var at = text.IndexOf('@', 0, end);
        if (at >= 0)
            end = at;

        return end >= 2 ? text.Substring(0, end) : null;
    }

    private static bool TryParseCallbackData(string data, out string key, out string[] args)
    {
        // Hot path: avoid Split + LINQ allocations.
        // Format: "{key} {arg1} {arg2} ..."; multiple spaces are ignored.
        if (string.IsNullOrWhiteSpace(data))
        {
            key = string.Empty;
            args = Array.Empty<string>();
            return false;
        }

        var len = data.Length;
        var i = 0;

        // Skip leading spaces
        while (i < len && data[i] == ' ')
            i++;

        if (i >= len)
        {
            key = string.Empty;
            args = Array.Empty<string>();
            return false;
        }

        // Read key token
        var keyStart = i;
        while (i < len && data[i] != ' ')
            i++;

        var keyLen = i - keyStart;
        if (keyLen <= 0)
        {
            key = string.Empty;
            args = Array.Empty<string>();
            return false;
        }

        key = data.Substring(keyStart, keyLen);

        // First pass: count args
        var count = 0;
        while (i < len)
        {
            while (i < len && data[i] == ' ')
                i++;

            if (i >= len)
                break;

            count++;
            while (i < len && data[i] != ' ')
                i++;
        }

        if (count == 0)
        {
            args = Array.Empty<string>();
            return true;
        }

        // Second pass: materialize args
        args = new string[count];
        i = keyStart + keyLen;
        var a = 0;

        while (i < len)
        {
            while (i < len && data[i] == ' ')
                i++;

            if (i >= len)
                break;

            var argStart = i;
            while (i < len && data[i] != ' ')
                i++;

            args[a++] = data.Substring(argStart, i - argStart);
        }

        return true;
    }
}
