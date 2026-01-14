using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TelegramBotKit.DependencyInjection;
using TelegramBotKit.Fallbacks;
using TelegramBotKit.Hosting;
using TelegramBotKit.Sample.ConsolePolling;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

var bot = builder.Services.AddTelegramBotKit(opt =>
{
    builder.Configuration.GetSection("TelegramBotKit").Bind(opt);
});

builder.Services.AddCommandsFromAssemblies(Assembly.GetExecutingAssembly());

builder.Services.AddSingleton<IDefaultMessageHandler, SampleDefaultHandlers>();
builder.Services.AddSingleton<IDefaultCallbackHandler, SampleDefaultHandlers>();
builder.Services.AddSingleton<IDefaultUpdateHandler, SampleDefaultHandlers>();

bot.UseMiddleware<TraceLoggingMiddleware>();


bot.UseQueuedMessageSender(o =>
{
    o.GlobalMaxPerSecond = 25;
    o.PerChatMinDelay = TimeSpan.FromSeconds(1);
    o.MaxQueueSize = 2000;
    o.MaxRetryAttempts = 5;
});

builder.Services.AddTelegramBotKitPolling();

var host = builder.Build();
await host.RunAsync();
