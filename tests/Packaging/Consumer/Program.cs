using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot.Types;
using TelegramBotKit;
using TelegramBotKit.Commands;
using TelegramBotKit.DependencyInjection;
using TelegramBotKit.Dispatching;
using TelegramBotKit.Keyboards;

var services = new ServiceCollection();
var calls = new List<string>();
services.AddSingleton(calls);
services.AddLogging();
using var http = new HttpClient(new DenyNetwork());
services.AddTelegramBotKit(o => o.Token = "123456:PACKAGING_OFFLINE_TOKEN", http);
services.AddCommands().AddCommands();
await using var provider = services.BuildServiceProvider();
var dispatcher = provider.GetRequiredService<IUpdateDispatcher>();
foreach (var text in new[] { "/app", "hello" })
    await dispatcher.DispatchAsync(new Update { Id = 1, Message = new Message
        { Id = 1, Chat = new Chat { Id = 42 }, From = new User { Id = 7 }, Text = text } });
await dispatcher.DispatchAsync(new Update { Id = 2, CallbackQuery = new CallbackQuery
    { Id = "callback", From = new User { Id = 7 }, Data = "app-key 42" } });
if (!calls.SequenceEqual(new[] { "message", "text", "callback:42" }))
    throw new InvalidOperationException("Generated registration mismatch: " + string.Join(",", calls));
if (Keyboard.Callback<AppCallback>("button").CallbackData != "app-key")
    throw new InvalidOperationException("Generated callback key mismatch.");
Console.WriteLine("PACKED_GENERATION_OK");

[MessageCommand("/app")]
internal sealed class AppCommand : IMessageCommand
{
    public Task HandleAsync(Message message, BotContext ctx)
    {
        ctx.GetRequiredService<List<string>>().Add("message");
        return Task.CompletedTask;
    }
}
[TextCommand("hello")]
internal sealed class AppText : ITextCommand
{
    public Task HandleAsync(Message message, BotContext ctx)
    {
        ctx.GetRequiredService<List<string>>().Add("text");
        return Task.CompletedTask;
    }
}
[CallbackCommand("app-key")]
internal sealed class AppCallback : ICallbackCommand
{
    public Task HandleAsync(CallbackQuery query, string[] args, BotContext ctx)
    {
        ctx.GetRequiredService<List<string>>().Add("callback:" + args.Single());
        return Task.CompletedTask;
    }
}
internal sealed class DenyNetwork : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
        throw new InvalidOperationException("Network access is forbidden in packaging validation.");
}
