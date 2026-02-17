using System.Collections.Concurrent;

namespace TelegramBotKit.Commands;

internal static class CallbackCommandKeyResolver
{
    private static readonly ConcurrentDictionary<RuntimeTypeHandle, string> _cache = new();

    public static string GetKey<THandler>() where THandler : ICallbackCommand
        => GetKey(typeof(THandler));

    public static string GetKey(Type t)
    {
        // 1) если TelegramBotKit.Generators установлен — будет resolver
        if (TelegramBotKitGeneratedCallbackKeysHook.TryGetKey(t, out var key))
            return key;

        // 2) иначе fallback на reflection (с кэшем)
        return _cache.GetOrAdd(t.TypeHandle, _ => ReadFromAttribute(t));
    }

    private static string ReadFromAttribute(Type t)
    {
        var attr = (CallbackCommandAttribute?)Attribute.GetCustomAttribute(
            t, typeof(CallbackCommandAttribute), inherit: false);

        if (attr is null)
            throw new InvalidOperationException($"{t.FullName} must have [CallbackCommand(\"key\")].");

        return attr.Key;
    }
}
