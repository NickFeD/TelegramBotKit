namespace TelegramBotKit.Middleware;

using TelegramBotKit.Pipelines;

/// <summary>
/// Provides a bot context delegate.
/// </summary>
public delegate Task BotContextDelegate(BotContext ctx);

internal sealed class MiddlewarePipeline
{
    private readonly Pipeline<BotContext> _pipeline;

    public MiddlewarePipeline(
        IEnumerable<Func<IServiceProvider, IUpdateMiddleware>> middlewareFactories)
    {
        var nodes = (middlewareFactories ?? Array.Empty<Func<IServiceProvider, IUpdateMiddleware>>())
            .Select(static factory => (IPipelineNode<BotContext>)new DelegatePipelineNode<BotContext>(
                (context, next) => factory(context.Services).InvokeAsync(
                    context,
                    nextContext => next(nextContext))))
            .ToArray();

        _pipeline = new Pipeline<BotContext>(nodes);
    }

    public BotContextDelegate Build(BotContextDelegate terminal)
    {
        ArgumentNullException.ThrowIfNull(terminal);
        var app = _pipeline.Build(context => terminal(context));
        return context => app(context);
    }
}
