using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using TelegramBotKit.Commands;
using TelegramBotKit.DependencyInjection;
using Xunit;

namespace TelegramBotKit.Tests;

[CollectionDefinition("Generated registration", DisableParallelization = true)]
public sealed class GeneratedRegistrationCollection;

// The production integration point is process-wide; isolate tests and restore the real registrar.
[Collection("Generated registration")]
public sealed class GeneratedRegistrationTests : IDisposable
{
    private static readonly FieldInfo Registrar = typeof(TelegramBotKitGeneratedCommandsHook)
        .GetField("_registrar", BindingFlags.NonPublic | BindingFlags.Static)!;
    private readonly object? _previous = Registrar.GetValue(null);

    [Fact]
    public void Generated_commands_can_be_registered_in_independent_containers()
    {
        TelegramBotKitGeneratedCommandsHook.SetRegistrar(Register);
        var first = new ServiceCollection();
        var second = new ServiceCollection();

        first.AddCommands(fallbackToReflection: false);
        second.AddCommands(fallbackToReflection: false);

        Assert.Single(first, d => d.ServiceType == typeof(MessageCommandDescriptor));
        Assert.Single(second, d => d.ServiceType == typeof(MessageCommandDescriptor));
    }

    [Fact]
    public void Generated_registration_is_idempotent_within_one_container()
    {
        TelegramBotKitGeneratedCommandsHook.SetRegistrar(Register);
        var services = new ServiceCollection();
        services.AddCommands(fallbackToReflection: false);
        services.AddCommands(fallbackToReflection: false);
        Assert.Single(services, d => d.ServiceType == typeof(MessageCommandDescriptor));
    }

    [Fact]
    public void Failed_registrar_is_not_marked_as_successfully_applied()
    {
        var attempts = 0;
        TelegramBotKitGeneratedCommandsHook.SetRegistrar(services =>
        {
            if (++attempts == 1) throw new InvalidOperationException("registration failed");
            Register(services);
        });
        var services = new ServiceCollection();
        Assert.Throws<InvalidOperationException>(() => services.AddCommands(fallbackToReflection: false));
        services.AddCommands(fallbackToReflection: false);
        Assert.Equal(2, attempts);
        Assert.Single(services, d => d.ServiceType == typeof(MessageCommandDescriptor));
    }

    private static void Register(IServiceCollection services) =>
        services.AddMessageCommand<CommandMatchingTests.RecordingCommand>("/generated");

    [Fact]
    public void Recursive_registration_fails_clearly_and_does_not_poison_the_collection()
    {
        TelegramBotKitGeneratedCommandsHook.SetRegistrar(services => services.AddCommands(fallbackToReflection: false));
        var services = new ServiceCollection();
        var error = Assert.Throws<InvalidOperationException>(() => services.AddCommands(fallbackToReflection: false));
        Assert.Contains("recursively", error.Message);
        TelegramBotKitGeneratedCommandsHook.SetRegistrar(Register);
        services.AddCommands(fallbackToReflection: false);
        Assert.Single(services, d => d.ServiceType == typeof(MessageCommandDescriptor));
    }

    public void Dispose() => Registrar.SetValue(null, _previous);
}
