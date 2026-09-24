using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using TelegramBotKit.DependencyInjection;
using TelegramBotKit.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", optional: false);

builder.Services.AddTelegramBotKit(options =>
    builder.Configuration.GetSection("TelegramBotKit").Bind(options));
builder.Services.AddCommands();
builder.Services.AddTelegramBotKitPolling();

await builder.Build().RunAsync();
