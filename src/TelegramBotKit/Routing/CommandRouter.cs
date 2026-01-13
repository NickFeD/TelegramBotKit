using Telegram.Bot.Types;
using TelegramBotKit.Commands;

namespace TelegramBotKit.Routing;

/// <summary>
/// Роутит Message/CallbackQuery в зарегистрированные команды.
/// </summary>
public sealed class CommandRouter
{
    private readonly IReadOnlyList<IMessageCommand> _messageCommands;
    private readonly IReadOnlyList<ITextCommand> _textCommands;
    private readonly IReadOnlyList<ICallbackCommand> _callbackCommands;

    public CommandRouter(
        IEnumerable<IMessageCommand> messageCommands,
        IEnumerable<ITextCommand> textCommands,
        IEnumerable<ICallbackCommand> callbackCommands)
    {
        _messageCommands = messageCommands?.ToList() ?? throw new ArgumentNullException(nameof(messageCommands));
        _textCommands = textCommands?.ToList() ?? throw new ArgumentNullException(nameof(textCommands));
        _callbackCommands = callbackCommands?.ToList() ?? throw new ArgumentNullException(nameof(callbackCommands));
    }

    /// <summary>
    /// Back-compat: старый метод. Теперь просто вызывает Try-версию.
    /// </summary>
    public async Task RouteMessageAsync(Message message, BotContext ctx, CancellationToken ct)
        => _ = await TryRouteMessageAsync(message, ctx, ct).ConfigureAwait(false);

    /// <summary>
    /// Возвращает true, если сообщение было обработано (message-командой или text-командой).
    /// </summary>
    public async Task<bool> TryRouteMessageAsync(Message message, BotContext ctx, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var text = message.Text;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        text = text.Trim();

        // 1) Slash-команда: /start или /start@BotName
        if (text.Length > 1 && text[0] == '/')
        {
            var cmd = ExtractSlashCommand(text);
            if (cmd is null)
                return false;

            var handler = _messageCommands.FirstOrDefault(c =>
                string.Equals(NormalizeSlash(c.Command), cmd, StringComparison.OrdinalIgnoreCase));

            if (handler is null)
                return false;

            await handler.HandleAsync(message, ctx, ct).ConfigureAwait(false);
            return true;
        }

        // 2) Текстовые команды (триггеры)
        // Выбираем "лучшее" совпадение: самый длинный триггер (чтобы "меню настройки" победило "меню").
        ITextCommand? best = null;
        var bestLen = -1;

        foreach (var cmd in _textCommands)
        {
            foreach (var trig in cmd.Triggers)
            {
                if (string.IsNullOrWhiteSpace(trig))
                    continue;

                var ok = cmd.IgnoreCase
                    ? string.Equals(text, trig, StringComparison.OrdinalIgnoreCase)
                    : string.Equals(text, trig, StringComparison.Ordinal);

                if (ok && trig.Length > bestLen)
                {
                    best = cmd;
                    bestLen = trig.Length;
                }
            }
        }

        if (best is null)
            return false;

        await best.HandleAsync(message, ctx, ct).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Back-compat: старый метод. Теперь просто вызывает Try-версию.
    /// </summary>
    public async Task RouteCallbackAsync(CallbackQuery query, BotContext ctx, CancellationToken ct)
        => _ = await TryRouteCallbackAsync(query, ctx, ct).ConfigureAwait(false);

    /// <summary>
    /// Возвращает true, если callback был обработан.
    /// </summary>
    public async Task<bool> TryRouteCallbackAsync(CallbackQuery query, BotContext ctx, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var data = query.Data;
        if (string.IsNullOrWhiteSpace(data))
            return false;

        if (!TryParseCallbackData(data, out var key, out var args))
            return false;

        var handler = _callbackCommands.FirstOrDefault(c =>
            string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase));

        if (handler is null)
            return false;

        await handler.HandleAsync(query, args, ctx, ct).ConfigureAwait(false);
        return true;
    }

    private static string? ExtractSlashCommand(string text)
    {
        // Берём первый "токен" до пробела
        var first = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries)[0];
        if (first.Length < 2 || first[0] != '/')
            return null;

        // /start@MyBot -> /start
        var at = first.IndexOf('@');
        if (at >= 0)
            first = first[..at];

        return first;
    }

    private static string NormalizeSlash(string cmd)
    {
        if (string.IsNullOrWhiteSpace(cmd))
            return string.Empty;

        cmd = cmd.Trim();

        if (cmd[0] != '/')
            cmd = "/" + cmd;

        var at = cmd.IndexOf('@');
        if (at >= 0)
            cmd = cmd[..at];

        return cmd;
    }

    private static bool TryParseCallbackData(string data, out string key, out string[] args)
    {
        // Формат: "like 123" или "like:123" — сейчас поддержим только пробелы как ты хотел (key + args)
        // Если захочешь ":"-формат — легко добавить тут.
        var parts = data.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            key = string.Empty;
            args = Array.Empty<string>();
            return false;
        }

        key = parts[0];
        args = parts.Length > 1 ? parts.Skip(1).ToArray() : Array.Empty<string>();
        return !string.IsNullOrWhiteSpace(key);
    }
}
