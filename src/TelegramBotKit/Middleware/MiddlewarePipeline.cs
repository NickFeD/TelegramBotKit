namespace TelegramBotKit.Middleware;

using TelegramBotKit.Pipelines;

/// <summary>
/// Continues global middleware processing with the supplied update context.
/// </summary>
/// <param name="ctx">The current update context, including scoped services and cancellation.</param>
/// <returns>The asynchronous operation, which completes after processing finishes.</returns>
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
