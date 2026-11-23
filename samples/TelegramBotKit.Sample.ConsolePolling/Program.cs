using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using TelegramBotKit.DependencyInjection;
using TelegramBotKit.Dispatching;
using TelegramBotKit.Fallbacks;
using TelegramBotKit.Polling;
using TelegramBotKit.Sample.ConsolePolling;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders().AddConsole();

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .AddCommandLine(args);

builder.Services.AddSingleton<IDefaultMessageHandler, SampleDefaultHandlers>();
builder.Services.AddSingleton<IDefaultCallbackHandler, SampleDefaultHandlers>();
builder.Services.AddSingleton<IDefaultUpdateHandler, SampleDefaultHandlers>();

var bot = builder.Services.AddTelegramBotKit(opt =>
{
    builder.Configuration.GetSection("TelegramBotKit").Bind(opt);
});

builder.Services.AddCommands();
bot.UseMiddleware<TraceLoggingMiddleware>();

bot.Route(UpdateRoutes.EditedMessage)
    .Use<EditedMessageAuditMiddleware>()
    .HandleWith<EditedMessageHandler>();
bot.UseQueuedMessageSender(o =>
{
    o.GlobalMaxPerSecond = 25;
    o.PerChatMinDelay = TimeSpan.FromSeconds(1);
    o.MaxQueueSize = 2000;
    o.MaxRetryAttempts = 5;
});

builder.Services.AddTelegramBotKitPolling();

var host = builder.Build();
try
{
    await host.RunAsync();
}
catch (OptionsValidationException error)
{
    Console.Error.WriteLine("Configuration error: " + string.Join("; ", error.Failures));
    Environment.ExitCode = 1;
}
