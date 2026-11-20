using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramBotKit.DependencyInjection;
using TelegramBotKit.Dispatching;
using TelegramBotKit.Hosting;
using TelegramBotKit.Polling;
using TelegramBotKit.Webhook.AspNetCore;

namespace TelegramBotKit.Benchmarks;

public enum WebhookMode { AdapterOnly, FullCoreAndHosting }
public class WebhookBenchmarks
{
    [ParamsAllValues] public WebhookMode Mode { get; set; }
    private WebApplication _app = null!;
    private HttpClient _client = null!;
    private readonly byte[] _body = Encoding.UTF8.GetBytes(Fixtures.UpdateJson);
    private readonly BatchState _state = new();
    private readonly Update _update = Fixtures.Update();
    private IUpdateReceiver _receiver = null!;

    [GlobalSetup] public async Task Setup()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.Logging.ClearProviders();
        builder.WebHost.UseTestServer();
        var bot = builder.Services.AddTelegramBotKit(o => o.Token = Fixtures.Token, new HttpClient(new OfflineApi()));
        builder.Services.AddSingleton(_state);
        if (Mode == WebhookMode.AdapterOnly) builder.Services.AddSingleton<IUpdateReceiver>(new CompletingReceiver(_state));
        else
        {
            bot.Route(UpdateRoutes.Message).HandleWith<SchedulerRuntime.WorkHandler>();
            var descriptor = builder.Services.Single(d => d.ServiceType == typeof(IUpdateDispatcher));
            builder.Services.Remove(descriptor);
            builder.Services.AddSingleton<IUpdateDispatcher>(sp => new CompletingDispatcher(
                (IUpdateDispatcher)ActivatorUtilities.CreateInstance(sp, descriptor.ImplementationType!), _state));
        }
        builder.Services.AddTelegramBotKitWebhook(o => o.SecretToken = "benchmark-secret");
        _app = builder.Build();
        _app.MapTelegramBotKitWebhook();
        await _app.StartAsync();
        _client = _app.GetTestClient();
        _receiver = _app.Services.GetRequiredService<IUpdateReceiver>();
        await AuthenticatedPostAndCompletion();
    }

    [Benchmark(Baseline = true)] public async Task ReceiverAndCompletion()
    {
        var finished = _state.Begin(1);
        await _receiver.ReceiveAsync(_update);
        await finished.WaitAsync(TimeSpan.FromSeconds(30));
    }
    [Benchmark] public async Task AuthenticatedPostAndCompletion()
    {
        var finished = _state.Begin(1);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/telegram/update") { Content = new ByteArrayContent(_body) };
        request.Content.Headers.ContentType = new("application/json");
        request.Headers.Add("X-Telegram-Bot-Api-Secret-Token", "benchmark-secret");
        using var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        await finished.WaitAsync(TimeSpan.FromSeconds(30));
    }
    [Benchmark] public async Task<HttpStatusCode> RejectedSecret()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/telegram/update") { Content = new ByteArrayContent(_body) };
        request.Content.Headers.ContentType = new("application/json");
        using var response = await _client.SendAsync(request);
        if (response.StatusCode != HttpStatusCode.Forbidden) throw new InvalidOperationException("Expected forbidden response.");
        return response.StatusCode;
    }
    [GlobalCleanup] public async Task Cleanup()
    {
        await _app.StopAsync();
        _client.Dispose();
        await _app.DisposeAsync();
    }
    private sealed class CompletingReceiver(BatchState state) : IUpdateReceiver
    {
        public ValueTask ReceiveAsync(Update update, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            state.Complete(update);
            return ValueTask.CompletedTask;
        }
    }
}

public class JsonBenchmarks
{
    [Params(1, 100)] public int BatchSize { get; set; }
    private byte[] _json = null!;
    [GlobalSetup] public void Setup() => _json = Encoding.UTF8.GetBytes("[" + string.Join(",", Enumerable.Repeat(Fixtures.UpdateJson, BatchSize)) + "]");
    [Benchmark] public Update[]? DeserializeTelegramUpdates() => JsonSerializer.Deserialize<Update[]>(_json, JsonBotAPI.Options);
}

public class PollingBenchmarks
{
    [Params(1, 100)] public int BatchSize { get; set; }
    private ServiceProvider _provider = null!;
    private IHostedService[] _hosted = null!;
    private HttpClient _http = null!;
    private PollingApi _api = null!;
    private readonly BatchState _state = new();

    [GlobalSetup] public async Task Setup()
    {
        _api = new PollingApi();
        _http = new HttpClient(_api);
        var (services, _) = Fixtures.Services(_http);
        services.AddSingleton<IUpdateReceiver>(new CountingReceiver(_state));
        services.AddTelegramBotKitPolling();
        _provider = services.BuildServiceProvider();
        _hosted = _provider.GetServices<IHostedService>().ToArray();
        foreach (var hosted in _hosted) await hosted.StartAsync(default);
        _api.Body = "{\"ok\":true,\"result\":[" + string.Join(",", Enumerable.Repeat(Fixtures.UpdateJson, BatchSize)) + "]}";
        await GetUpdatesBatchAndAdmission();
    }
    // One operation is one complete GetUpdates response batch, not one update.
    [Benchmark] public async Task GetUpdatesBatchAndAdmission()
    {
        var finished = _state.Begin(BatchSize);
        await _api.Responses.Writer.WriteAsync(true);
        await finished.WaitAsync(TimeSpan.FromSeconds(30));
    }
    [GlobalCleanup] public async Task Cleanup()
    {
        foreach (var hosted in _hosted.Reverse()) await hosted.StopAsync(default);
        await _provider.DisposeAsync();
        _http.Dispose();
    }
    private sealed class CountingReceiver(BatchState state) : IUpdateReceiver
    {
        public ValueTask ReceiveAsync(Update update, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            state.Complete(update);
            return ValueTask.CompletedTask;
        }
    }
    private sealed class PollingApi : HttpMessageHandler
    {
        internal readonly Channel<bool> Responses = Channel.CreateBounded<bool>(1);
        internal string Body = null!;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (!request.RequestUri!.AbsolutePath.EndsWith("getUpdates", StringComparison.Ordinal)) throw new InvalidOperationException("Unexpected Telegram method.");
            await Responses.Reader.ReadAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Body, Encoding.UTF8, "application/json") };
        }
    }
}
