using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot.Types;
using TelegramBotKit.Commands;
using TelegramBotKit.DependencyInjection;
using TelegramBotKit.Dispatching;
using Xunit;

namespace TelegramBotKit.Tests;

public sealed class CommandMatchingTests
{
    [Theory]
    [InlineData("slash", "/HELLO", true)]
    [InlineData("slash", "/Hello@MyBot argument", true)]
    [InlineData("callback", "HELLO one two", true)]
    [InlineData("text", " HELLO ", true)]
    [InlineData("exact", "hello", true)]
    [InlineData("exact", "HELLO", false)]
    [InlineData("text", "unknown", false)]
    public async Task Dispatch_preserves_configured_command_comparison(string kind, string input, bool matches)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<List<string>>();
        services.AddTelegramBotKit(o => o.Token = "123456:TEST_TOKEN");
        if (kind == "slash") services.AddMessageCommand<RecordingCommand>("/hello");
        else if (kind == "callback") services.AddCallbackCommand<RecordingCommand>("hello");
        else services.AddTextCommand<RecordingCommand>(["hello"], ignoreCase: kind != "exact");
        await using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        var message = new Message { Chat = new Chat { Id = 42 }, From = new User { Id = 7 }, Text = input };
        var update = kind == "callback"
            ? new Update { CallbackQuery = new CallbackQuery { Data = input, Message = message } }
            : new Update { Message = message };

        await provider.GetRequiredService<IUpdateDispatcher>().DispatchAsync(update);

        Assert.Equal(matches ? new[] { kind == "callback" ? "one,two" : "called" } : [],
            provider.GetRequiredService<List<string>>());
    }

    [Theory]
    [InlineData("slash")]
    [InlineData("callback")]
    [InlineData("text")]
    public void Duplicate_keys_are_rejected_independently_of_case(string kind)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTelegramBotKit(o => o.Token = "123456:TEST_TOKEN");
        foreach (var key in new[] { "hello", "HELLO" })
        {
            if (kind == "slash") services.AddMessageCommand<RecordingCommand>(key);
            else if (kind == "callback") services.AddCallbackCommand<RecordingCommand>(key);
            else services.AddTextCommand<RecordingCommand>([key]);
        }
        using var provider = services.BuildServiceProvider();
        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<CommandRegistry>());
    }

    public sealed class RecordingCommand(List<string> calls) : IMessageCommand, ITextCommand, ICallbackCommand
    {
        public Task HandleAsync(Message message, BotContext ctx)
        {
            calls.Add("called");
            return Task.CompletedTask;
        }

        public Task HandleAsync(CallbackQuery query, string[] args, BotContext ctx)
        {
            calls.Add(string.Join(',', args));
            return Task.CompletedTask;
        }
    }
}
