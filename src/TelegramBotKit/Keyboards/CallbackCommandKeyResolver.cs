using System.Collections.Concurrent;

namespace TelegramBotKit.Commands;

internal static class CallbackCommandKeyResolver
{
    public static string GetKey<THandler>() where THandler : ICallbackCommand
        => GetKey(typeof(THandler));

    public static string GetKey(Type t)
    {
        ArgumentNullException.ThrowIfNull(t);
        if (TelegramBotKitGeneratedCallbackKeysHook.TryGetKey(t, out var key))
            return key;

        throw new InvalidOperationException($"No generated callback key for {t.FullName}. Add [CallbackCommand] and enable the generator bundled with TelegramBotKit, or supply the key explicitly.");
    }
}
