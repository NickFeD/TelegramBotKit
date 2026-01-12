using TelegramBotKit.Middleware;

namespace TelegramBotKit.DependencyInjection;

internal sealed class DelegateMiddlewareConfigurator : IMiddlewareConfigurator
{
    private readonly Action<MiddlewarePipeline> _action;
    public DelegateMiddlewareConfigurator(Action<MiddlewarePipeline> action) => _action = action;
    public void Configure(MiddlewarePipeline pipeline) => _action(pipeline);
}
