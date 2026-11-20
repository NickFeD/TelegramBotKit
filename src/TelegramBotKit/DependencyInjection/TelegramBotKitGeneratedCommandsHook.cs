using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;

namespace TelegramBotKit.DependencyInjection;

/// <summary>
/// Connects the bundled compiler-generated registrar to Core's command registration.
/// This public hook supports generated code and is not an application extension point.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class TelegramBotKitGeneratedCommandsHook
{
    private static Action<IServiceCollection>? _registrar;
    private static readonly ConditionalWeakTable<IServiceCollection, RegistrationState> Registrations = new();

    /// <summary>
    /// Called by generated code to replace the current registrar delegate.
    /// Each delegate is applied at most once per service collection after a successful invocation.
    /// </summary>
    /// <remarks>This replaces the process-wide delegate; registrars from multiple assemblies are not combined.</remarks>
    /// <param name="registrar">The generated delegate that replaces the current command registrar.</param>
    public static void SetRegistrar(Action<IServiceCollection> registrar)
        => Volatile.Write(ref _registrar, registrar ?? throw new ArgumentNullException(nameof(registrar)));

    internal static bool TryRun(IServiceCollection services)
    {
        var registrar = Volatile.Read(ref _registrar);
        if (registrar is null)
            return false;

        var state = Registrations.GetValue(services, static _ => new RegistrationState());
        lock (state)
        {
            if (state.Applied.Contains(registrar)) return true;
            if (state.Registering)
                throw new InvalidOperationException("Generated command registration cannot recursively call AddCommands.");
            state.Registering = true;
            try
            {
                registrar(services);
                state.Applied.Add(registrar);
            }
            finally
            {
                state.Registering = false;
            }
        }
        return true;
    }

    private sealed class RegistrationState
    {
        internal HashSet<Action<IServiceCollection>> Applied { get; } = new();
        internal bool Registering { get; set; }
    }
}
