using System.ComponentModel;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;

namespace TelegramBotKit.DependencyInjection;

/// <summary>
/// Internal hook used by TelegramBotKit.Generators to provide generated command registrations.
/// Not intended for direct use.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class TelegramBotKitGeneratedCommandsHook
{
    private static Action<IServiceCollection>? _registrar;

    /// <summary>
    /// Called by generated code to provide a registrar delegate.
    /// </summary>
    public static void SetRegistrar(Action<IServiceCollection> registrar)
        => _registrar = registrar ?? throw new ArgumentNullException(nameof(registrar));

    internal static bool TryRun(IServiceCollection services)
    {
        // "Consume" once to avoid double registrations when AddCommands() is called multiple times.
        var registrar = Interlocked.Exchange(ref _registrar, null);
        if (registrar is null)
            return false;

        registrar(services);
        return true;
    }
}
