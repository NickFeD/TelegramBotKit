namespace TelegramBotKit.Pipelines;

internal delegate Task PipelineDelegate<TContext>(TContext context);

internal interface IPipelineNode<TContext>
{
    Task InvokeAsync(TContext context, PipelineDelegate<TContext> next);
}

/// <summary>Composes a snapshot of ordered nodes around a terminal delegate.</summary>
internal sealed class Pipeline<TContext>
{
    private readonly IPipelineNode<TContext>[] _nodes;

    public Pipeline(IEnumerable<IPipelineNode<TContext>> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        _nodes = nodes.ToArray();
    }

    public PipelineDelegate<TContext> Build(PipelineDelegate<TContext> terminal)
    {
        ArgumentNullException.ThrowIfNull(terminal);

        var app = terminal;
        for (var i = _nodes.Length - 1; i >= 0; i--)
        {
            var node = _nodes[i];
            var next = app;
            app = context => node.InvokeAsync(context, next);
        }

        return app;
    }
}
