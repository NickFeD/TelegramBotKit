using BenchmarkDotNet.Attributes;
using TelegramBotKit.Pipelines;

namespace TelegramBotKit.Benchmarks;

public class PipelineBenchmarks
{
    [Params(0, 1, 5, 20)] public int Depth { get; set; }
    private PipelineDelegate<object> _app = null!;
    private readonly object _context = new();
    private IPipelineNode<object>[] _nodes = null!;
    private static readonly PipelineDelegate<object> Terminal = _ => Task.CompletedTask;
    [GlobalSetup] public void Setup()
    {
        _nodes = Enumerable.Range(0, Depth).Select(_ => (IPipelineNode<object>)
            new DelegatePipelineNode<object>(static (ctx, next) => next(ctx))).ToArray();
        _app = new Pipeline<object>(_nodes).Build(Terminal);
    }
    [Benchmark(Baseline = true)] public Task DirectDelegate() => Terminal(_context);
    [Benchmark] public Task CompiledPipeline() => _app(_context);
    [Benchmark] public object BuildPipeline() => new Pipeline<object>(_nodes).Build(Terminal);
}
