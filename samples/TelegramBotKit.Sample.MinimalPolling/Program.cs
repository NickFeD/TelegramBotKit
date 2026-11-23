using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using TelegramBotKit.DependencyInjection;
using TelegramBotKit.Polling;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders().AddConsole();

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile("appsettings.Local.json", optional: true)
    .AddEnvironmentVariables()
    .AddCommandLine(args);

builder.Services.AddTelegramBotKit(options =>
    builder.Configuration.GetSection("TelegramBotKit").Bind(options));
builder.Services.AddCommands();
builder.Services.AddTelegramBotKitPolling();

try
{
    await builder.Build().RunAsync();
}
catch (OptionsValidationException error)
{
    Console.Error.WriteLine("Configuration error: " + string.Join("; ", error.Failures));
    Environment.ExitCode = 1;
}
