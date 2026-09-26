using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TelegramBotKit.DependencyInjection;
using TelegramBotKit.Fallbacks;
using TelegramBotKit.Hosting;
using TelegramBotKit.Routing;
using TelegramBotKit.Sample.PhotoSelector.Access;
using TelegramBotKit.Sample.PhotoSelector.Bot;
using TelegramBotKit.Sample.PhotoSelector.Browsing;
using TelegramBotKit.Sample.PhotoSelector.Data;
using TelegramBotKit.Sample.PhotoSelector.Infrastructure;
using TelegramBotKit.Sample.PhotoSelector.Media;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
builder.Configuration.AddUserSecrets("TelegramBotKit.Sample.PhotoSelector");

var botToken = builder.Configuration["TELEGRAM_BOT_TOKEN"];
if (string.IsNullOrWhiteSpace(botToken))
    botToken = builder.Configuration["TelegramBotKit:Token"];
if (string.IsNullOrWhiteSpace(botToken))
{
    Console.Error.WriteLine(
        "Токен Telegram-бота не настроен.\n" +
        "Задайте TELEGRAM_BOT_TOKEN или выполните:\n" +
        "dotnet user-secrets set \"TelegramBotKit:Token\" \"<token>\" " +
        "--project samples/TelegramBotKit.Sample.PhotoSelector");
    Environment.ExitCode = 1;
    return;
}

var connectionString = builder.Configuration.GetConnectionString("PhotoSelector")
    ?? throw new InvalidOperationException("Не задана строка подключения ConnectionStrings:PhotoSelector.");
var dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;
if (!string.IsNullOrWhiteSpace(dataSource) && dataSource != ":memory:")
{
    var fullPath = Path.GetFullPath(dataSource, builder.Environment.ContentRootPath);
    Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
    connectionString = new SqliteConnectionStringBuilder(connectionString) { DataSource = fullPath }.ToString();
}

builder.Services.AddDbContextFactory<PhotoSelectorDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddScoped<AccessService>();
builder.Services.AddScoped<SourceChatService>();
builder.Services.AddScoped<MediaIndexService>();
builder.Services.AddScoped<SelectionService>();
builder.Services.AddScoped<BrowseService>();
builder.Services.AddScoped<ExportService>();
builder.Services.AddScoped<MediaPreviewService>();
builder.Services.AddScoped<BotUiService>();
builder.Services.AddScoped<BotCommandMenuService>();
builder.Services.AddScoped<PhotoSelectorCommands>();
builder.Services.AddScoped<IDefaultMessageHandler, DefaultMessageHandler>();

var bot = builder.Services.AddTelegramBotKit(options =>
{
    builder.Configuration.GetSection("TelegramBotKit").Bind(options);
    options.Token = botToken;
});

bot.UseMiddleware<UpdateIngestionMiddleware>();
bot.UseMessageCommand<PhotoSelectorCommands>("/start", (message, ctx, service) => service.StartAsync(message, ctx));
bot.UseMessageCommand<PhotoSelectorCommands>("/whoami", (message, ctx, service) => service.WhoAmIAsync(message, ctx));
bot.UseMessageCommand<PhotoSelectorCommands>("/access_grant", (message, ctx, service) => service.GrantAsync(message, ctx));
bot.UseMessageCommand<PhotoSelectorCommands>("/access_revoke", (message, ctx, service) => service.RevokeAsync(message, ctx));
bot.UseMessageCommand<PhotoSelectorCommands>("/access_list", (message, ctx, service) => service.ListAccessAsync(message, ctx));
bot.UseMessageCommand<PhotoSelectorCommands>("/source_enable", (message, ctx, service) => service.EnableSourceAsync(message, ctx));
bot.UseMessageCommand<PhotoSelectorCommands>("/source_disable", (message, ctx, service) => service.DisableSourceAsync(message, ctx));
bot.UseMessageCommand<PhotoSelectorCommands>("/browse", (message, ctx, service) => service.BrowseAsync(message, ctx));
bot.UseMessageCommand<PhotoSelectorCommands>("/liked", (message, ctx, service) => service.LikedAsync(message, ctx));
bot.UseMessageCommand<PhotoSelectorCommands>("/skipped", (message, ctx, service) => service.SkippedAsync(message, ctx));
bot.UseMessageCommand<PhotoSelectorCommands>("/all", (message, ctx, service) => service.AllAsync(message, ctx));
bot.UseMessageCommand<PhotoSelectorCommands>("/topics", (message, ctx, service) => service.TopicsAsync(message, ctx));
bot.UseMessageCommand<PhotoSelectorCommands>("/sources", (message, ctx, service) => service.SourcesAsync(message, ctx));
bot.UseMessageCommand<PhotoSelectorCommands>("/export", (message, ctx, service) => service.ExportAsync(message, ctx));
bot.UseCallbackCommand<PhotoSelectorCommands>("ps", (query, arguments, ctx, service) =>
    service.HandleCallbackAsync(query, arguments, ctx));

builder.Services.AddTelegramBotKitPolling();

var host = builder.Build();
await using (var scope = host.Services.CreateAsyncScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PhotoSelectorDbContext>>();
    await using var db = await factory.CreateDbContextAsync();
    await db.Database.MigrateAsync();

    var commandMenu = scope.ServiceProvider.GetRequiredService<BotCommandMenuService>();
    await commandMenu.ResetAndConfigureAsync();
}

await host.RunAsync();
