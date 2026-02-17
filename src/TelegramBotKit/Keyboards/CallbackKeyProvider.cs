using System.Collections.Concurrent;
using TelegramBotKit.Commands;

namespace TelegramBotKit.Keyboards;

public static class CallbackKeyProvider
{
    private static readonly ConcurrentDictionary<Type, string> _cache = new();

    public static string GetKey<THandler>() where THandler : ICallbackCommand
        => _cache.GetOrAdd(typeof(THandler), _ => Read(typeof(THandler)));

    private static string Read(Type t)
    {
        var attr = (CallbackCommandAttribute?)Attribute.GetCustomAttribute(t, typeof(CallbackCommandAttribute));
        if (attr is null)
            throw new InvalidOperationException($"{t.FullName} must have [CallbackCommand(\"key\")]");

        return attr.Key;
    }
}
