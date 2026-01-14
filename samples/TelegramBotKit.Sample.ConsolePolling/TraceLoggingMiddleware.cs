using Microsoft.Extensions.Logging;
using TelegramBotKit.Middleware;

namespace TelegramBotKit.Sample.ConsolePolling;

public sealed class TraceLoggingMiddleware : IUpdateMiddleware
{
    private readonly ILogger _log;

    public TraceLoggingMiddleware(ILoggerFactory loggerFactory)
    {
        if (loggerFactory is null) throw new ArgumentNullException(nameof(loggerFactory));
        _log = loggerFactory.CreateLogger("TelegramBotKit.Sample");
    }

    public async Task InvokeAsync(BotContext ctx, BotContextDelegate next)
    {
        var traceId = Guid.NewGuid().ToString("N");
        ctx.Items["traceId"] = traceId;

        _log.LogInformation(">> Update {Type}, trace={Trace}",
            ctx.Update.Type,
            traceId);

        try
        {
            await next(ctx).ConfigureAwait(false);
            _log.LogInformation("<< Done trace={Trace}", traceId);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "<< Error trace={Trace}", traceId);
            throw;
        }
    }
}
