namespace TelegramBotKit.Pipelines;

internal sealed class DelegatePipelineNode<TContext> : IPipelineNode<TContext>
{
    private readonly Func<TContext, PipelineDelegate<TContext>, Task> _invoke;

    public DelegatePipelineNode(Func<TContext, PipelineDelegate<TContext>, Task> invoke)
    {
        ArgumentNullException.ThrowIfNull(invoke);
        _invoke = invoke;
    }

    public Task InvokeAsync(TContext context, PipelineDelegate<TContext> next) =>
        _invoke(context, next);
}
