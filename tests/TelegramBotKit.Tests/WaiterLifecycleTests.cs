using Telegram.Bot.Types;
using TelegramBotKit.Conversations;
using Xunit;

namespace TelegramBotKit.Tests;

public sealed class WaiterLifecycleTests
{
    [Fact]
    public async Task Concurrent_publishers_deliver_exactly_one_response()
    {
        var wait = new WaitForUserResponse();
        for (var iteration = 0; iteration < 100; iteration++)
        {
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var pending = wait.WaitAsync(42, 7, TimeSpan.FromMinutes(1), cancellation.Token);
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var publishers = Enumerable.Range(1, 8).Select(async id =>
            {
                await start.Task;
                return wait.TryPublish(Message(id));
            }).ToArray();
            start.SetResult();
            var accepted = await Task.WhenAll(publishers);
            Assert.Single(accepted, result => result);
            var received = await pending;
            Assert.NotNull(received);
            Assert.True(accepted[received.Id - 1]);
        }
    }

    [Fact]
    public async Task Completing_a_wait_does_not_remove_its_successor()
    {
        var wait = new WaitForUserResponse();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        for (var iteration = 0; iteration < 500; iteration++)
        {
            var first = wait.WaitAsync(42, 7, TimeSpan.FromMinutes(1), cancellation.Token);
            Assert.True(wait.TryPublish(Message(1)));
            var next = wait.WaitAsync(42, 7, TimeSpan.FromMinutes(1), cancellation.Token);
            Assert.Equal(1, (await first)!.Id);
            Assert.True(wait.TryPublish(Message(2)));
            Assert.Equal(2, (await next)!.Id);
        }
    }

    [Fact]
    public async Task Cancellation_returns_null_and_leaves_other_keys_unchanged()
    {
        var wait = new WaitForUserResponse();
        using var canceled = new CancellationTokenSource();
        var first = wait.WaitAsync(42, 7, TimeSpan.FromMinutes(1), canceled.Token);
        var other = wait.WaitAsync(43, 7, TimeSpan.FromMinutes(1));
        var duplicate = wait.WaitAsync(42, 7, TimeSpan.FromMinutes(1));
        await Assert.ThrowsAsync<InvalidOperationException>(() => duplicate);
        canceled.Cancel();
        Assert.Null(await first);
        Assert.False(wait.TryPublish(Message(1)));
        Assert.False(other.IsCompleted);
        Assert.True(wait.TryPublish(new Message { Id = 3, Chat = new Chat { Id = 43 }, From = new User { Id = 7 } }));
        Assert.Equal(3, (await other)!.Id);
    }

    private static Message Message(int id) => new() { Id = id, Chat = new Chat { Id = 42 }, From = new User { Id = 7 } };

    [Fact]
    public async Task Invalid_timeout_does_not_leave_a_stale_registration()
    {
        var wait = new WaitForUserResponse();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => wait.WaitAsync(42, 7, TimeSpan.MaxValue));
        var next = wait.WaitAsync(42, 7, TimeSpan.FromMinutes(1));
        Assert.True(wait.TryPublish(Message(3)));
        Assert.Equal(3, (await next)!.Id);
    }

    [Fact]
    public async Task Timeout_returns_null_at_deadline_and_releases_the_key()
    {
        var clock = new TestTimeProvider();
        var wait = new WaitForUserResponse(clock);
        var pending = wait.WaitAsync(42, 7, TimeSpan.FromSeconds(10));
        clock.Advance(TimeSpan.FromMilliseconds(9999));
        Assert.False(pending.IsCompleted);
        clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Null(await pending.WaitAsync(TimeSpan.FromSeconds(5)));
        var next = wait.WaitAsync(42, 7, TimeSpan.FromMinutes(1));
        Assert.True(wait.TryPublish(Message(4)));
        Assert.Equal(4, (await next)!.Id);
    }

    [Fact]
    public async Task Already_canceled_wait_returns_null_without_occupying_the_key()
    {
        var wait = new WaitForUserResponse();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Null(await wait.WaitAsync(42, 7, TimeSpan.FromMinutes(1), cancellation.Token));
        Assert.False(wait.TryPublish(Message(1)));
        var next = wait.WaitAsync(42, 7, TimeSpan.FromMinutes(1));
        Assert.True(wait.TryPublish(Message(2)));
        Assert.Equal(2, (await next)!.Id);
    }

    [Fact]
    public async Task Cancellation_racing_publication_has_one_consistent_outcome()
    {
        var wait = new WaitForUserResponse();
        for (var iteration = 0; iteration < 200; iteration++)
        {
            using var cancellation = new CancellationTokenSource();
            var pending = wait.WaitAsync(42, 7, TimeSpan.FromMinutes(1), cancellation.Token);
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var publish = Task.Run(async () => { await start.Task; return wait.TryPublish(Message(5)); });
            var cancel = Task.Run(async () => { await start.Task; cancellation.Cancel(); });
            start.SetResult();
            await Task.WhenAll(publish, cancel);
            var response = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(await publish, response is not null);
            if (response is not null) Assert.Equal(5, response.Id);
        }
    }
}
