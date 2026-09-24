using TelegramBotKit.Pipelines;
using Xunit;

namespace TelegramBotKit.Tests;

public sealed class PipelineTests
{
    private static IPipelineNode<List<string>> Around(string name) =>
        new DelegatePipelineNode<List<string>>(async (events, next) =>
        {
            events.Add($"{name} before");
            try
            {
                await next(events);
            }
            finally
            {
                events.Add($"{name} after");
            }
        });

    [Fact]
    public async Task First_registered_node_is_outermost()
    {
        var pipeline = new Pipeline<List<string>>([Around("A"), Around("B")]);
        var app = pipeline.Build(async events =>
        {
            await Task.Yield();
            events.Add("terminal");
        });
        var events = new List<string>();

        await app(events);

        Assert.Equal(new[] { "A before", "B before", "terminal", "B after", "A after" }, events);
    }

    [Fact]
    public async Task Node_can_stop_downstream_execution()
    {
        var stop = new DelegatePipelineNode<List<string>>((events, _) =>
        {
            events.Add("stop");
            return Task.CompletedTask;
        });
        var app = new Pipeline<List<string>>([Around("A"), stop, Around("B")])
            .Build(events =>
            {
                events.Add("terminal");
                return Task.CompletedTask;
            });
        var events = new List<string>();

        await app(events);

        Assert.Equal(new[] { "A before", "stop", "A after" }, events);
    }

    [Fact]
    public async Task Terminal_exception_unwinds_and_propagates_unchanged()
    {
        var expected = new InvalidOperationException("terminal failed");
        var app = new Pipeline<List<string>>([Around("A"), Around("B")])
            .Build(async events =>
            {
                await Task.Yield();
                events.Add("throw");
                throw expected;
            });
        var events = new List<string>();

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => app(events));

        Assert.Same(expected, actual);
        Assert.Equal(new[] { "A before", "B before", "throw", "B after", "A after" }, events);
    }

    [Fact]
    public async Task Empty_pipeline_returns_terminal_unchanged()
    {
        PipelineDelegate<List<string>> terminal = events =>
        {
            events.Add("terminal");
            return Task.CompletedTask;
        };
        var app = new Pipeline<List<string>>([]).Build(terminal);
        var events = new List<string>();

        Assert.Same(terminal, app);
        await app(events);

        Assert.Equal(new[] { "terminal" }, events);
    }

    [Fact]
    public async Task Pipeline_snapshots_nodes_and_compiled_delegate_can_be_reused()
    {
        var nodes = new List<IPipelineNode<List<string>>> { Around("A") };
        var pipeline = new Pipeline<List<string>>(nodes);
        nodes.Clear();
        var app = pipeline.Build(events =>
        {
            events.Add("terminal");
            return Task.CompletedTask;
        });

        for (var i = 0; i < 2; i++)
        {
            var events = new List<string>();
            await app(events);
            Assert.Equal(new[] { "A before", "terminal", "A after" }, events);
        }
    }
}
