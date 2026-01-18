using System.Collections.Frozen;
using Telegram.Bot.Types;

namespace TelegramBotKit.Commands;

// NOTE: These delegates are internal by design.
// They give us an AOT-friendly seam:
// - today invokers may resolve handlers by Type (reflection-based scanning)
// - tomorrow a source-generator can emit invokers that resolve via generics (no reflection)
internal delegate ValueTask MessageCommandInvoker(Message message, BotContext ctx);
internal delegate ValueTask TextCommandInvoker(Message message, BotContext ctx);
internal delegate ValueTask CallbackCommandInvoker(CallbackQuery query, string[] args, BotContext ctx);

internal sealed class CommandRegistry
{
    private readonly IReadOnlyDictionary<string, MessageCommandInvoker> _messageBySlash;
    private readonly IReadOnlyDictionary<string, CallbackCommandInvoker> _callbackByKey;
    private readonly IReadOnlyDictionary<string, TextCommandInvoker> _textExact;
    private readonly IReadOnlyDictionary<string, TextCommandInvoker> _textIgnoreCase;

    public CommandRegistry(
        IEnumerable<MessageCommandDescriptor> messageDescriptors,
        IEnumerable<TextCommandDescriptor> textDescriptors,
        IEnumerable<CallbackCommandDescriptor> callbackDescriptors)
    {
        if (messageDescriptors is null) throw new ArgumentNullException(nameof(messageDescriptors));
        if (textDescriptors is null) throw new ArgumentNullException(nameof(textDescriptors));
        if (callbackDescriptors is null) throw new ArgumentNullException(nameof(callbackDescriptors));

        var msg = new Dictionary<string, MessageCommandInvoker>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in messageDescriptors)
        {
            var key = NormalizeSlash(d.Command);
            if (!msg.TryAdd(key, d.Invoker))
                throw new InvalidOperationException($"Duplicate message command: '{key}'");
        }

        var cb = new Dictionary<string, CallbackCommandInvoker>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in callbackDescriptors)
        {
            var key = d.Key.Trim();
            if (!cb.TryAdd(key, d.Invoker))
                throw new InvalidOperationException($"Duplicate callback command: '{key}'");
        }

        // Registry is read-only after startup. Frozen dictionaries improve lookup speed.
        _messageBySlash = msg.ToFrozenDictionary();
        _callbackByKey = cb.ToFrozenDictionary();

        var textExact = new Dictionary<string, TextCommandInvoker>(StringComparer.Ordinal);
        var textIgnore = new Dictionary<string, TextCommandInvoker>(StringComparer.OrdinalIgnoreCase);

        foreach (var d in textDescriptors)
        {
            foreach (var raw in d.Triggers)
            {
                var trig = (raw ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(trig))
                    continue;

                if (d.IgnoreCase)
                {
                    if (!textIgnore.TryAdd(trig, d.Invoker))
                        throw new InvalidOperationException($"Duplicate text trigger (ignoreCase): '{trig}'");
                }
                else
                {
                    if (!textExact.TryAdd(trig, d.Invoker))
                        throw new InvalidOperationException($"Duplicate text trigger: '{trig}'");
                }
            }
        }

        foreach (var kv in textExact)
        {
            if (textIgnore.ContainsKey(kv.Key))
                throw new InvalidOperationException($"Text trigger '{kv.Key}' is registered both case-sensitive and ignoreCase.");
        }

        _textExact = textExact.ToFrozenDictionary();
        _textIgnoreCase = textIgnore.ToFrozenDictionary();
    }

    public bool TryGetMessageCommand(string slash, out MessageCommandInvoker invoker)
        => _messageBySlash.TryGetValue(NormalizeSlash(slash), out invoker!);

    /// <summary>
    /// Fast-path lookup for an already normalized slash command (trimmed, leading '/', no '@bot').
    /// </summary>
    public bool TryGetMessageCommandNormalized(string normalizedSlash, out MessageCommandInvoker invoker)
        => _messageBySlash.TryGetValue(normalizedSlash, out invoker!);

    public bool TryGetCallbackCommand(string key, out CallbackCommandInvoker invoker)
        => _callbackByKey.TryGetValue(key.Trim(), out invoker!);

    /// <summary>
    /// Fast-path lookup for an already trimmed callback key.
    /// </summary>
    public bool TryGetCallbackCommandNormalized(string trimmedKey, out CallbackCommandInvoker invoker)
        => _callbackByKey.TryGetValue(trimmedKey, out invoker!);

    public bool TryGetTextCommand(string text, out TextCommandInvoker invoker)
    {
        text = (text ?? string.Empty).Trim();

        if (string.IsNullOrEmpty(text))
        {
            invoker = null!;
            return false;
        }

        if (_textExact.TryGetValue(text, out invoker!))
            return true;

        return _textIgnoreCase.TryGetValue(text, out invoker!);
    }

    /// <summary>
    /// Fast-path lookup for an already trimmed non-empty text message.
    /// </summary>
    public bool TryGetTextCommandNormalized(string trimmedText, out TextCommandInvoker invoker)
    {
        if (string.IsNullOrEmpty(trimmedText))
        {
            invoker = null!;
            return false;
        }

        if (_textExact.TryGetValue(trimmedText, out invoker!))
            return true;

        return _textIgnoreCase.TryGetValue(trimmedText, out invoker!);
    }

    public static string NormalizeSlash(string cmd)
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
}

internal sealed record MessageCommandDescriptor(string Command, MessageCommandInvoker Invoker);

internal sealed record TextCommandDescriptor(IReadOnlyList<string> Triggers, bool IgnoreCase, TextCommandInvoker Invoker);

internal sealed record CallbackCommandDescriptor(string Key, CallbackCommandInvoker Invoker);
