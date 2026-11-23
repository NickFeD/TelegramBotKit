using Telegram.Bot.Types;
using Microsoft.Extensions.Options;
using TelegramBotKit;
using TelegramBotKit.Commands;
using TelegramBotKit.DependencyInjection;
using TelegramBotKit.Messaging;
using TelegramBotKit.Options;
using TelegramBotKit.Webhook.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddTelegramBotKit(options =>
    builder.Configuration.GetSection("TelegramBotKit").Bind(options));
builder.Services.AddMessageCommand<StartCommand>("/start");
builder.Services.AddTelegramBotKitWebhook(options =>
    builder.Configuration.GetSection("Webhook").Bind(options));

try
{
    var app = builder.Build();
    app.MapTelegramBotKitWebhook();

    // Explicit deployment operation. Starting or stopping the server alone never changes Telegram.
    if (builder.Configuration.GetValue<bool>("RegisterWebhook"))
        await app.Services.SetTelegramBotKitWebhookAsync();

    await app.RunAsync();
}
catch (OptionsValidationException error)
{
    Console.Error.WriteLine("Configuration error: " + string.Join("; ", error.Failures));
    Environment.ExitCode = 1;
}

/// <summary>Replies to /start to demonstrate webhook command delivery.</summary>
public sealed class StartCommand : IMessageCommand
{
    /// <summary>Sends a confirmation to the chat that invoked the command.</summary>
    /// <param name="message">The incoming command message.</param>
    /// <param name="ctx">The update scope, sender and processing cancellation token.</param>
    /// <returns>The asynchronous send operation.</returns>
    public Task HandleAsync(Message message, BotContext ctx) =>
        ctx.Sender.SendText(message.Chat.Id, new SendText { Text = "Webhook is working." }, ctx.CancellationToken);
}
