using Microsoft.Extensions.Logging;
using TelegramBotKit.Middleware;

namespace TelegramBotKit.Sample.ConsolePolling;

public sealed class SampleMiddlewareConfigurator : IMiddlewareConfigurator
{
    public void Configure(MiddlewarePipeline pipeline)
    {
        pipeline.Use(async (ctx, next) =>
        {
            var logger = ctx.GetRequiredService<ILoggerFactory>().CreateLogger("TelegramBotKit.Sample");

            var traceId = Guid.NewGuid().ToString("N");
            ctx.Items["traceId"] = traceId;

            logger.LogInformation(">> Update {Type}, trace={Trace}", ctx.Update.Type, traceId);

            try
            {
                await next().ConfigureAwait(false);
                logger.LogInformation("<< Done trace={Trace}", traceId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "<< Error trace={Trace}", traceId);
                throw;
            }
        });
    }
}
