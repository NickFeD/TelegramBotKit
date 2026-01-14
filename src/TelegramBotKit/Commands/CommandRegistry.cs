namespace TelegramBotKit.Commands;

internal sealed class CommandRegistry
{
    private readonly IReadOnlyDictionary<string, Type> _messageBySlash;
    private readonly IReadOnlyDictionary<string, Type> _callbackByKey;
    private readonly IReadOnlyDictionary<string, Type> _textExact;
    private readonly IReadOnlyDictionary<string, Type> _textIgnoreCase;

    public CommandRegistry(
        IEnumerable<MessageCommandDescriptor> messageDescriptors,
        IEnumerable<TextCommandDescriptor> textDescriptors,
        IEnumerable<CallbackCommandDescriptor> callbackDescriptors)
    {
        if (messageDescriptors is null) throw new ArgumentNullException(nameof(messageDescriptors));
        if (textDescriptors is null) throw new ArgumentNullException(nameof(textDescriptors));
        if (callbackDescriptors is null) throw new ArgumentNullException(nameof(callbackDescriptors));

        var msg = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in messageDescriptors)
        {
            var key = NormalizeSlash(d.Command);
            if (!msg.TryAdd(key, d.CommandType))
                throw new InvalidOperationException($"Duplicate message command: '{key}'");
        }

        var cb = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in callbackDescriptors)
        {
            var key = d.Key.Trim();
            if (!cb.TryAdd(key, d.CommandType))
                throw new InvalidOperationException($"Duplicate callback command: '{key}'");
        }

        _messageBySlash = msg;
        _callbackByKey = cb;

        var textExact = new Dictionary<string, Type>(StringComparer.Ordinal);
        var textIgnore = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

        foreach (var d in textDescriptors)
        {
            foreach (var raw in d.Triggers)
            {
                var trig = (raw ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(trig))
                    continue;

                if (d.IgnoreCase)
                {
                    if (!textIgnore.TryAdd(trig, d.CommandType))
                        throw new InvalidOperationException($"Duplicate text trigger (ignoreCase): '{trig}'");
                }
                else
                {
                    if (!textExact.TryAdd(trig, d.CommandType))
                        throw new InvalidOperationException($"Duplicate text trigger: '{trig}'");
                }
            }
        }

        foreach (var kv in textExact)
        {
            if (textIgnore.ContainsKey(kv.Key))
                throw new InvalidOperationException($"Text trigger '{kv.Key}' is registered both case-sensitive and ignoreCase.");
        }

        _textExact = textExact;
        _textIgnoreCase = textIgnore;
    }

    public bool TryGetMessageCommand(string slash, out Type commandType)
        => _messageBySlash.TryGetValue(NormalizeSlash(slash), out commandType!);

    public bool TryGetCallbackCommand(string key, out Type commandType)
        => _callbackByKey.TryGetValue(key.Trim(), out commandType!);

    public bool TryGetTextCommand(string text, out Type commandType)
    {
        text = (text ?? string.Empty).Trim();

        if (string.IsNullOrEmpty(text))
        {
            commandType = null!;
            return false;
        }

        if (_textExact.TryGetValue(text, out commandType!))
            return true;

        return _textIgnoreCase.TryGetValue(text, out commandType!);
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

internal sealed record MessageCommandDescriptor(string Command, Type CommandType);

internal sealed record TextCommandDescriptor(IReadOnlyList<string> Triggers, bool IgnoreCase, Type CommandType);

internal sealed record CallbackCommandDescriptor(string Key, Type CommandType);
