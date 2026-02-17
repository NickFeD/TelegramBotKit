using System.ComponentModel;

namespace TelegramBotKit.Commands;

[EditorBrowsable(EditorBrowsableState.Never)]
public static class TelegramBotKitGeneratedCallbackKeysHook
{
    private static Func<Type, string?>? _resolver;

    /// <summary>Вызывается сгенерированным кодом.</summary>
    public static void SetResolver(Func<Type, string?> resolver)
        => _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));

    internal static bool TryGetKey(Type handlerType, out string key)
    {
        var r = _resolver;
        if (r is null)
        {
            key = "";
            return false;
        }

        var k = r(handlerType);
        if (string.IsNullOrWhiteSpace(k))
        {
            key = "";
            return false;
        }

        key = k!;
        return true;
    }
}
