using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot.Types;
using TelegramBotKit.DependencyInjection;
using TelegramBotKit.Messaging;
using Xunit;

namespace TelegramBotKit.Tests;

public sealed class MessagingTests
{
    public static IEnumerable<object?[]> Destinations()
    {
        foreach (var queued in new[] { false, true })
        foreach (var photo in new[] { false, true })
        foreach (var operation in new[] { "chat", "message", "callback", "trySend", "reply", "callbackReply", "tryReply" })
        foreach (var sourceThread in new int?[] { null, 100 })
        foreach (var explicitThread in new int?[] { null, 200 })
            yield return new object?[] { queued, photo, operation, sourceThread, explicitThread };
    }

    [Theory]
    [MemberData(nameof(Destinations))]
    public async Task Sends_preserve_destination_and_request(
        bool queued, bool photo, string operation, int? sourceThread, int? explicitThread)
    {
        using var handler = new CaptureHandler();
        using var http = new HttpClient(handler);
        await using var provider = CreateProvider(http, queued);
        var sender = provider.GetRequiredService<IMessageSender>();
        var source = new Message { Id = 42, Chat = new Chat { Id = -100123 }, MessageThreadId = sourceThread };
        var callback = new CallbackQuery { Id = "callback", Message = source };
        var text = new SendText { Text = "Hello", MessageThreadId = explicitThread, DisableNotification = true, ProtectContent = true };
        var picture = new SendPhoto { Photo = InputFile.FromFileId("photo-id"), Caption = "Caption", MessageThreadId = explicitThread, HasSpoiler = true };

        Task<Message>? task;
        if (photo)
        {
            switch (operation)
            {
                case "chat": task = sender.SendPhoto(source.Chat.Id, picture); break;
                case "message": task = sender.SendPhoto(source, picture); break;
                case "callback": task = sender.SendPhoto(callback, picture); break;
                case "trySend": Assert.True(sender.TrySendPhoto(callback, picture, out task)); break;
                case "reply": task = sender.ReplyPhoto(source, picture); break;
                case "callbackReply": task = sender.ReplyPhoto(callback, picture); break;
                default: Assert.True(sender.TryReplyPhoto(callback, picture, out task)); break;
            }
        }
        else
        {
            switch (operation)
            {
                case "chat": task = sender.SendText(source.Chat.Id, text); break;
                case "message": task = sender.SendText(source, text); break;
                case "callback": task = sender.SendText(callback, text); break;
                case "trySend": Assert.True(sender.TrySendText(callback, text, out task)); break;
                case "reply": task = sender.ReplyText(source, text); break;
                case "callbackReply": task = sender.ReplyText(callback, text); break;
                default: Assert.True(sender.TryReplyText(callback, text, out task)); break;
            }
        }

        Assert.NotNull(task);
        await task.WaitAsync(TimeSpan.FromSeconds(5));
        var body = Assert.Single(handler.Requests);
        Assert.Equal(source.Chat.Id, body.GetProperty("chat_id").GetInt64());
        var expectedThread = explicitThread ?? (operation == "chat" ? null : sourceThread);
        Assert.Equal(expectedThread, body.TryGetProperty("message_thread_id", out var thread) ? thread.GetInt32() : (int?)null);
        var isReply = operation is "reply" or "callbackReply" or "tryReply";
        Assert.Equal(isReply, body.TryGetProperty("reply_parameters", out var reply));
        if (isReply) Assert.Equal(source.Id, reply.GetProperty("message_id").GetInt32());
        Assert.Equal(photo ? "sendPhoto" : "sendMessage", handler.Method);
        if (photo)
        {
            Assert.Equal("photo-id", body.GetProperty("photo").GetString());
            Assert.Equal("Caption", body.GetProperty("caption").GetString());
            Assert.True(body.GetProperty("has_spoiler").GetBoolean());
        }
        else
        {
            Assert.Equal("Hello", body.GetProperty("text").GetString());
            Assert.True(body.GetProperty("disable_notification").GetBoolean());
            Assert.True(body.GetProperty("protect_content").GetBoolean());
        }
        Assert.Equal(explicitThread, text.MessageThreadId);
        Assert.Equal(explicitThread, picture.MessageThreadId);
        Assert.Equal(sourceThread, source.MessageThreadId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Inline_callbacks_fail_fast_or_return_false_without_sending(bool queued)
    {
        using var handler = new CaptureHandler();
        using var http = new HttpClient(handler);
        await using var provider = CreateProvider(http, queued);
        var sender = provider.GetRequiredService<IMessageSender>();
        var callback = new CallbackQuery { Id = "inline", InlineMessageId = "inline-message" };
        var text = new SendText { Text = "Hello" };
        var photo = new SendPhoto { Photo = InputFile.FromFileId("photo-id") };

        Assert.Throws<NotSupportedException>(() => { _ = sender.SendText(callback, text); });
        Assert.Throws<NotSupportedException>(() => { _ = sender.ReplyText(callback, text); });
        Assert.Throws<NotSupportedException>(() => { _ = sender.SendPhoto(callback, photo); });
        Assert.Throws<NotSupportedException>(() => { _ = sender.ReplyPhoto(callback, photo); });
        Assert.False(sender.TrySendText(callback, text, out var sendText));
        Assert.False(sender.TryReplyText(callback, text, out var replyText));
        Assert.False(sender.TrySendPhoto(callback, photo, out var sendPhoto));
        Assert.False(sender.TryReplyPhoto(callback, photo, out var replyPhoto));
        Assert.Null(sendText);
        Assert.Null(replyText);
        Assert.Null(sendPhoto);
        Assert.Null(replyPhoto);
        Assert.Empty(handler.Requests);
    }

    private static ServiceProvider CreateProvider(HttpClient http, bool queued)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var bot = services.AddTelegramBotKit(o => o.Token = "123456:TEST_TOKEN", http);
        if (queued) bot.UseQueuedMessageSender(o =>
        {
            o.GlobalMaxPerSecond = 0;
            o.PerChatMinDelay = TimeSpan.Zero;
        });
        return services.BuildServiceProvider();
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public List<JsonElement> Requests { get; } = new();
        public string? Method { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Method = request.RequestUri!.Segments[^1];
            using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            Requests.Add(json.RootElement.Clone());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"ok":true,"result":{"message_id":43,"date":1,"chat":{"id":-100123,"type":"supergroup"}}}""", Encoding.UTF8, "application/json")
            };
        }
    }
}
