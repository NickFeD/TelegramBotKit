using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TelegramBotKit.DependencyInjection;
using TelegramBotKit.Hosting.DependencyInjection;

await Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration(cfg =>
    {
        cfg.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
        cfg.AddEnvironmentVariables();
        cfg.AddUserSecrets<Program>(optional: true);
    })
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();
        logging.SetMinimumLevel(LogLevel.Information);
    })
    .ConfigureServices((ctx, services) =>
    {
        // Core
        var builder = services.AddTelegramBotKit(opt =>
        {
            // Можно хранить токен в appsettings.json или в UserSecrets/ENV
            ctx.Configuration.GetSection("TelegramBotKit").Bind(opt);

            // на всякий случай: если токен не подхватился из конфига
            if (string.IsNullOrWhiteSpace(opt.Token))
                opt.Token = ctx.Configuration["TELEGRAM_TOKEN"] ?? string.Empty;

            // polling настройки
            // opt.Polling.MaxDegreeOfParallelism = 1; // если хочешь строго по порядку
        });

        // Автоподхват команд с [Command] из сборки sample
        builder.AddCommandsFromAssembly<StartCommand>();

        builder.Use(async (ctx, next) =>
        {
            var log = ctx.GetRequiredService<ILoggerFactory>().CreateLogger("TBK");
            var sw = System.Diagnostics.Stopwatch.StartNew();

            // пример Items
            ctx.Items["traceId"] = Guid.NewGuid().ToString("N");

            log.LogInformation("-> Update {Type} (traceId={traceId})",
                ctx.Update.Type, ctx.Items["traceId"]);

            try
            {
                await next();
                log.LogInformation("<- Done {Type} ({ms}ms) (traceId={traceId})",
                    ctx.Update.Type, sw.ElapsedMilliseconds, ctx.Items["traceId"]);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "!! Error {Type} (traceId={traceId})",
                    ctx.Update.Type, ctx.Items["traceId"]);
                throw;
            }
        });


        // Hosting (Polling)
        services.AddTelegramBotKitPolling();
    })
    .Build()
    .RunAsync();
