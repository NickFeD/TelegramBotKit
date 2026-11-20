using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot.Types;
using TelegramBotKit.Commands;
using TelegramBotKit.DependencyInjection;
using TelegramBotKit.Dispatching;
using TelegramBotKit.Keyboards;
using TelegramBotKit.Routing;
using Xunit;

namespace TelegramBotKit.Tests;

[Collection("Generated registration")]
public sealed class GeneratedOnlyDiscoveryTests : IDisposable
{
    private static readonly FieldInfo Registrar = typeof(TelegramBotKitGeneratedCommandsHook).GetField("_registrar", BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly FieldInfo Resolver = typeof(TelegramBotKitGeneratedCallbackKeysHook).GetField("_resolver", BindingFlags.NonPublic | BindingFlags.Static)!;
    private readonly object? _registrar = Registrar.GetValue(null), _resolver = Resolver.GetValue(null);
    public GeneratedOnlyDiscoveryTests() { Registrar.SetValue(null, null); Resolver.SetValue(null, null); }

    [Fact]
    public void Default_discovery_does_not_scan_attributed_types()
    {
        var services = new ServiceCollection();
        Assert.Same(services, services.AddCommands());
        Assert.Empty(services);
    }

    [Fact]
    public void Strict_discovery_still_rejects_missing_generated_registration()
    {
        var error = Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddCommands(fallbackToReflection: false));
        Assert.Contains("No generated command registrations", error.Message);
    }

    [Fact]
    public void Callback_attribute_does_not_trigger_reflection_fallback()
    {
        Assert.Contains("No generated callback key", Assert.Throws<InvalidOperationException>(() => Keyboard.Callback<Attributed>("button")).Message);
        Assert.Equal("explicit 42", Keyboard.Callback("button", "explicit", "42").CallbackData);
    }

    [Fact]
    public void Generated_callback_mapping_is_still_used()
    {
        TelegramBotKitGeneratedCallbackKeysHook.SetResolver(t => t == typeof(Attributed) ? "generated" : null);
        Assert.Equal("generated 42", Keyboard.Callback<Attributed>("button", "42").CallbackData);
    }

    [Fact]
    public async Task Explicit_generic_and_fluent_commands_work_without_generator()
    {
        var calls = new List<string>();
        var services = new ServiceCollection();
        services.AddSingleton(calls);
        services.AddLogging();
        var bot = services.AddTelegramBotKit(o => o.Token = "123456:OFFLINE_TEST_TOKEN");
        services.AddMessageCommand<Explicit>("/generic");
        bot.UseMessageCommand("/fluent", (Message _, BotContext _) => { calls.Add("fluent"); return ValueTask.CompletedTask; });
        await using var provider = services.BuildServiceProvider();
        foreach (var text in new[] { "/generic", "/fluent" })
            await provider.GetRequiredService<IUpdateDispatcher>().DispatchAsync(new Update
                { Id = 1, Message = new Message { Id = 1, Chat = new Chat { Id = 42 }, Text = text } });
        Assert.Equal(new[] { "generic", "fluent" }, calls);
    }

    public void Dispose() { Registrar.SetValue(null, _registrar); Resolver.SetValue(null, _resolver); }
    public sealed class Explicit : IMessageCommand
    {
        public Task HandleAsync(Message message, BotContext ctx)
        {
            ctx.GetRequiredService<List<string>>().Add("generic");
            return Task.CompletedTask;
        }
    }
    [MessageCommand("/attribute-only")]
    [CallbackCommand("attribute-only")]
    public sealed class Attributed : IMessageCommand, ICallbackCommand
    {
        public Task HandleAsync(Message message, BotContext ctx) => Task.CompletedTask;
        public Task HandleAsync(CallbackQuery query, string[] args, BotContext ctx) => Task.CompletedTask;
    }
}
