using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TelegramBotKit.DependencyInjection;
using TelegramBotKit.Fallbacks;
using TelegramBotKit.Hosting;          // <-- проект TelegramBotKit.Hosting
using TelegramBotKit.Middleware;
using TelegramBotKit.Sample.ConsolePolling;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

builder.Services.AddTelegramBotKit(opt =>
{
    builder.Configuration.GetSection("TelegramBotKit").Bind(opt);
});

// Команды из текущей сборки sample
builder.Services.AddCommandsFromAssemblies(Assembly.GetExecutingAssembly());

// Default handlers (чтобы видеть fallback)
builder.Services.AddSingleton<IDefaultMessageHandler, SampleDefaultHandlers>();
builder.Services.AddSingleton<IDefaultCallbackHandler, SampleDefaultHandlers>();
builder.Services.AddSingleton<IDefaultUpdateHandler, SampleDefaultHandlers>();

// Middleware (лог + traceId в ctx.Items)
builder.Services.AddSingleton<IMiddlewareConfigurator, SampleMiddlewareConfigurator>();

// Дополнительные маппинги UpdateType -> payload (пример расширения)
builder.Services.AddSingleton<TelegramBotKit.Dispatching.IRegistryConfigurator, SampleRegistryConfigurator>();

// Запуск polling
builder.Services.AddHostedService<PollingHostedService>();

var host = builder.Build();
await host.RunAsync();
