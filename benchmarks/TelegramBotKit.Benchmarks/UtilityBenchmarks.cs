using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramBotKit.DependencyInjection;
using TelegramBotKit.Keyboards;
using TelegramBotKit.Messaging;
using TelegramBotKit.Dispatching;
using TelegramBotKit.Routing;

namespace TelegramBotKit.Benchmarks;

public class MessagingBenchmarks
{
    private ServiceProvider _direct = null!, _queued = null!;
    private IMessageSender _sender = null!, _queue = null!;
    private ITelegramBotClient _client = null!;
    private readonly SendText _text = new() { Text = "benchmark message" };
    private readonly Message _reply = Fixtures.Update().Message!;
    private HttpClient _http = null!;
    [GlobalSetup] public async Task Setup()
    {
        _http = new HttpClient(new OfflineApi());
        var (services, _) = Fixtures.Services(_http);
        _direct = services.BuildServiceProvider();
        _sender = _direct.GetRequiredService<IMessageSender>();
        _client = _direct.GetRequiredService<ITelegramBotClient>();
        var (queued, bot) = Fixtures.Services(_http);
        bot.UseQueuedMessageSender(o => { o.GlobalMaxPerSecond = 0; o.PerChatMinDelay = TimeSpan.Zero; });
        _queued = queued.BuildServiceProvider();
        _queue = _queued.GetRequiredService<IMessageSender>();
        await _sender.SendText(42, _text);
        await _queue.SendText(42, _text);
    }
    [Benchmark(Baseline = true)] public Task<Message> RawTelegramClientOffline() => _client.SendMessage(42, _text.Text);
    [Benchmark] public Task<Message> SenderOffline() => _sender.SendText(42, _text);
    [Benchmark] public Task<Message> ReplyOffline() => _sender.ReplyText(_reply, _text);
    [Benchmark] public Task<Message> QueuedOfflineWithoutRateLimit() => _queue.SendText(42, _text);
    [GlobalCleanup] public async Task Cleanup()
    {
        await _queued.DisposeAsync();
        await _direct.DisposeAsync();
        _http.Dispose();
    }
}

public class KeyboardBenchmarks
{
    [Params(1, 10)] public int Rows { get; set; }
    private InlineKeyboardButton[][] _inline = null!;
    private KeyboardButton[][] _reply = null!;
    private readonly string[] _args = ["123", "456", "next"];
    private readonly string _key = "select";
    [GlobalSetup] public void Setup()
    {
        _inline = Enumerable.Range(0, Rows).Select(i => new[] { Keyboard.Callback("select", _key, i.ToString()), Keyboard.Url("link", "https://example.com") }).ToArray();
        _reply = Enumerable.Range(0, Rows).Select(_ => new[] { Keyboard.Text("yes"), Keyboard.Text("no") }).ToArray();
    }
    [Benchmark] public InlineKeyboardButton CallbackPack() => Keyboard.Callback("select", _key, _args);
    [Benchmark] public InlineKeyboardMarkup Inline() => Keyboard.Inline(_inline);
    [Benchmark] public ReplyKeyboardMarkup Reply() => Keyboard.Reply(_reply);
}

public class StartupBenchmarks
{
    [Params(10, 1000)] public int Commands { get; set; }
    [Benchmark(Baseline = true)] public void GenericRegistrationAndFirstDispatchResolution()
    {
        var (services, _) = Fixtures.Services();
        for (var i = 0; i < Commands; i++) services.AddMessageCommand<NoopCommand>("/command" + i);
        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<IUpdateDispatcher>();
    }
    [Benchmark] public void FluentRegistrationAndFirstDispatchResolution()
    {
        var (services, bot) = Fixtures.Services();
        for (var i = 0; i < Commands; i++) bot.UseMessageCommand("/command" + i, static (_, _) => ValueTask.CompletedTask);
        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<IUpdateDispatcher>();
    }
}
