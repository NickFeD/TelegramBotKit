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

    public async Task RouteMessageAsync(Message message, BotContext ctx, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var text = message.Text;
        if (string.IsNullOrWhiteSpace(text))
            return;

        text = text.Trim();

        // 1) Slash-команда: /start или /start@BotName
        if (text.Length > 1 && text[0] == '/')
        {
            var cmd = ExtractSlashCommand(text);
            if (cmd is null)
                return;

            // сравнение без учёта регистра, чтобы не зависеть от формата ввода
            var handler = _messageCommands.FirstOrDefault(c =>
                string.Equals(NormalizeSlash(c.Command), cmd, StringComparison.OrdinalIgnoreCase));

            if (handler is null)
                return;

            await handler.HandleAsync(message, ctx, ct).ConfigureAwait(false);
            return;
        }

        // 2) Текстовые команды (триггеры)
        // Здесь выберем "лучшее" совпадение: самый длинный триггер (чтобы "меню настройки" победило "меню").
        ITextCommand? best = null;
        int bestLen = -1;

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
            return;

        await best.HandleAsync(message, ctx, ct).ConfigureAwait(false);
    }

    public async Task RouteCallbackAsync(CallbackQuery query, BotContext ctx, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var data = query.Data;
        if (string.IsNullOrWhiteSpace(data))
            return;

        if (!TryParseCallbackData(data, out var key, out var args))
            return;

        var handler = _callbackCommands.FirstOrDefault(c =>
            string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase));

        if (handler is null)
            return;

        await handler.HandleAsync(query, args, ctx, ct).ConfigureAwait(false);
    }

    private static string? ExtractSlashCommand(string text)
    {
        // "/start@MyBot arg1 arg2" -> "/start"
        // "/start" -> "/start"
        // "/start " -> "/start"
        var firstToken = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(firstToken))
            return null;

        var at = firstToken.IndexOf('@');
        if (at > 0)
            firstToken = firstToken[..at];

        return NormalizeSlash(firstToken);
    }

    private static string NormalizeSlash(string cmd)
    {
        cmd = cmd.Trim();
        return cmd.StartsWith('/') ? cmd : "/" + cmd;
    }

    private static bool TryParseCallbackData(string data, out string key, out string[] args)
    {
        // "like 123" -> key="like", args=["123"]
        // "like" -> key="like", args=[]
        var parts = data.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0)
        {
            key = string.Empty;
            args = Array.Empty<string>();
            return false;
        }

        key = parts[0];
        args = parts.Length == 1 ? Array.Empty<string>() : parts.Skip(1).ToArray();
        return true;
    }
}
