using TelegramBotKit.Middleware;

namespace TelegramBotKit.DependencyInjection;

// ------------------- Internal configurators (доступны Builder'у) -------------------

internal interface IMiddlewareConfigurator
{
    void Configure(MiddlewarePipeline pipeline);
}
