using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot.Types;
using TelegramBotKit.Commands;
using TelegramBotKit.Routing;

namespace TelegramBotKit.Benchmarks;

public class CommandBenchmarks
{
    [Params(10, 1000)] public int Count { get; set; }
    private CommandRegistry _registry = null!;
    private CommandRouter _router = null!;
    private ServiceProvider _provider = null!;
    private BotContext _ctx = null!;
    private Message _slash = null!, _text = null!, _miss = null!;
    private CallbackQuery _callback = null!;
    private string _key = null!;
    [GlobalSetup] public void Setup()
    {
        _registry = new CommandRegistry(
            Enumerable.Range(0, Count).Select(i => new MessageCommandDescriptor("/cmd" + i, static (_, _) => ValueTask.CompletedTask)),
            Enumerable.Range(0, Count).Select(i => new TextCommandDescriptor(["text" + i], true, static (_, _) => ValueTask.CompletedTask)),
            Enumerable.Range(0, Count).Select(i => new CallbackCommandDescriptor("cmd" + i, static (_, _, _) => ValueTask.CompletedTask)));
        _router = new CommandRouter(_registry);
        var (services, _) = Fixtures.Services();
        _provider = services.BuildServiceProvider();
        _ctx = Fixtures.Context(_provider, Fixtures.Update());
        _key = "/CMD" + (Count - 1);
        _slash = Fixtures.Update(_key + "@BenchBot argument").Message!;
        _text = Fixtures.Update("TEXT" + (Count - 1)).Message!;
        _miss = Fixtures.Update("/missing").Message!;
        _callback = new CallbackQuery { Id = "callback", From = new User { Id = 7 }, Data = "cmd" + (Count - 1) + "  one  two three" };
        if (!_registry.TryGetMessageCommandNormalized(_key, out _)) throw new InvalidOperationException("Lookup fixture failed.");
    }
    [Benchmark] public bool RegistryHit() => _registry.TryGetMessageCommandNormalized(_key, out _);
    [Benchmark] public Task<bool> SlashWithMentionAndArgument() => _router.TryRouteMessageAsync(_slash, _ctx);
    [Benchmark] public Task<bool> TextIgnoreCase() => _router.TryRouteMessageAsync(_text, _ctx);
    [Benchmark] public Task<bool> MissingCommand() => _router.TryRouteMessageAsync(_miss, _ctx);
    [Benchmark] public Task<bool> CallbackThreeArguments() => _router.TryRouteCallbackAsync(_callback, _ctx);
    [GlobalCleanup] public void Cleanup() => _provider.Dispose();
}
