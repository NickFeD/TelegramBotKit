using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramBotKit.Commands;
using TelegramBotKit.DependencyInjection;
using TelegramBotKit.Dispatching;
using TelegramBotKit.Messaging;

namespace TelegramBotKit.Benchmarks;

internal static class Fixtures
{
    internal const string Token = "123456:BENCHMARK_OFFLINE_TOKEN";
    internal const string MessageJson = """
        {"message_id":1,"date":1,"chat":{"id":42,"type":"private"},"from":{"id":7,"is_bot":false,"first_name":"Bench"},"text":"/work"}
        """;
    internal static readonly string UpdateJson = "{\"update_id\":1,\"message\":" + MessageJson + "}";
    internal static Update Update(string text = "/work", long chat = 42, int id = 1) => new()
    {
        Id = id,
        Message = new Message { Id = id, Chat = new Chat { Id = chat }, From = new User { Id = 7, FirstName = "Bench" }, Text = text }
    };
    internal static (ServiceCollection Services, TelegramBotKitBuilder Bot) Services(HttpClient? http = null)
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.ClearProviders());
        if (http is null) services.AddSingleton(_ => new HttpClient(new OfflineApi()));
        var bot = services.AddTelegramBotKit(o => o.Token = Token, sp => http ?? sp.GetRequiredService<HttpClient>());
        return (services, bot);
    }
    internal static BotContext Context(ServiceProvider provider, Update update) => new(update,
        provider.GetRequiredService<ITelegramBotClient>(), provider.GetRequiredService<IMessageSender>(), provider, default);
}
// Local HTTP transport: fresh responses, no sockets or Telegram calls.
internal sealed class OfflineApi : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var result = request.RequestUri!.AbsolutePath.EndsWith("answerCallbackQuery", StringComparison.Ordinal)
            ? "true" : Fixtures.MessageJson;
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"ok\":true,\"result\":" + result + "}", Encoding.UTF8, "application/json")
        });
    }
}
public sealed class NoopCommand : IMessageCommand, ITextCommand, ICallbackCommand, IUpdatePayloadHandler<Message>
{
    public Task HandleAsync(Message message, BotContext ctx) => Task.CompletedTask;
    public Task HandleAsync(CallbackQuery query, string[] args, BotContext ctx) => Task.CompletedTask;
}
public sealed class NoopRouteMiddleware : IUpdateRouteMiddleware<Message>
{
    public Task InvokeAsync(UpdateRouteContext<Message> context, UpdateRouteDelegate<Message> next) => next(context);
}
