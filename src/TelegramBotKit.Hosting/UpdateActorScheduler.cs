using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using System.Threading.Channels;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramBotKit.Dispatching;
using TelegramBotKit.Options;

namespace TelegramBotKit.Hosting;

internal sealed class UpdateActorScheduler : IDisposable, IAsyncDisposable, IHostedService, IUpdateReceiver
{
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan CleanupPeriod = TimeSpan.FromMinutes(1);

    private readonly IUpdateDispatcher _dispatcher;
    private readonly ILogger<UpdateActorScheduler> _log;
    private readonly SemaphoreSlim? _dopSemaphore;

    private readonly object _gate = new();
    private readonly Dictionary<ActorKey, Actor> _actors = new();
    private readonly HashSet<Task> _active = new();
    private readonly CancellationTokenSource _shutdown = new();
    private readonly TimeProvider _timeProvider;
    private readonly ITimer _cleanup;
    private Task? _stopTask;
    private readonly Channel<ReceivedUpdate> _received = Channel.CreateUnbounded<ReceivedUpdate>(
        new UnboundedChannelOptions { SingleReader = true, AllowSynchronousContinuations = false });
    private readonly Task _receiverWorker;

    internal DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

    public UpdateActorScheduler(
        IUpdateDispatcher dispatcher,
        IOptions<TelegramBotKitOptions> options,
        ILogger<UpdateActorScheduler> log)
        : this(dispatcher, options, log, TimeProvider.System)
    {
    }

    internal UpdateActorScheduler(
        IUpdateDispatcher dispatcher,
        IOptions<TelegramBotKitOptions> options,
        ILogger<UpdateActorScheduler> log,
        TimeProvider timeProvider, int? maxDegreeOfParallelism = null)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

        var legacyDop = (options ?? throw new ArgumentNullException(nameof(options))).Value.Polling.MaxDegreeOfParallelism;
        var dop = maxDegreeOfParallelism ?? legacyDop;
        _dopSemaphore = dop > 0 ? new SemaphoreSlim(dop, dop) : null;

        _cleanup = _timeProvider.CreateTimer(_ => Cleanup(), null, CleanupPeriod, CleanupPeriod);
        _receiverWorker = Task.Run(ReceiveLoopAsync);
    }

    public ValueTask EnqueueAsync(Update update, CancellationToken ct)
        => EnqueueCore(update, ct, background: false);

    public ValueTask ReceiveAsync(Update update, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(update);
        ct.ThrowIfCancellationRequested();
        return EnqueueCore(update, CancellationToken.None, background: true);
    }

    private ValueTask EnqueueCore(Update update, CancellationToken ct, bool background)
    {
        if (update is null) throw new ArgumentNullException(nameof(update));
        ct.ThrowIfCancellationRequested();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationTokenSource cancellation;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_stopTask is not null, this);
            cancellation = CancellationTokenSource.CreateLinkedTokenSource(ct, _shutdown.Token);
            _active.Add(completion.Task);
            if (background)
                _received.Writer.TryWrite(new ReceivedUpdate(update, cancellation, completion));
        }
        if (!background)
            _ = ProcessTrackedAsync(update, cancellation, completion);
        return ValueTask.CompletedTask;
    }

    private async Task ReceiveLoopAsync()
    {
        await foreach (var item in _received.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            // Enter scheduling in admission order without awaiting handlers: a waiting
            // conversation must still be able to receive a subsequent update.
            _ = ProcessTrackedAsync(item.Update, item.Cancellation, item.Completion);
        }
    }

    private sealed record ReceivedUpdate(Update Update, CancellationTokenSource Cancellation,
        TaskCompletionSource Completion);

    private async Task ProcessTrackedAsync(Update update, CancellationTokenSource cancellation,
        TaskCompletionSource completion)
    {
        try
        {
            await ProcessAsync(update, cancellation.Token).ConfigureAwait(false);
        }
        finally
        {
            cancellation.Dispose();
            lock (_gate) _active.Remove(completion.Task);
            completion.TrySetResult();
        }
    }

    private async Task ScheduleAsync(Update update, Func<Task> work, CancellationToken ct)
    {
        var key = TryGetActorKey(update);

        if (key is null)
        {
            await RunWithGlobalLimitAsync(work, ct).ConfigureAwait(false);
            return;
        }

        var item = new UpdateWorkItem(ct, work);
        ValueTask admission;
        lock (_gate)
        {
            if (_stopTask is not null) throw new OperationCanceledException(ct);
            if (!_actors.TryGetValue(key.Value, out var actor))
            {
                actor = new Actor(this);
                _actors.Add(key.Value, actor);
            }
            // Admission and retirement share the same lock: no closed actor can be acquired.
            admission = actor.EnqueueAsync(item);
        }
        await admission.ConfigureAwait(false);
        await item.Completion.Task.ConfigureAwait(false);
    }

    private async Task ProcessAsync(Update update, CancellationToken ct)
    {
        try
        {
            if (_dispatcher is IScheduledUpdateDispatcher scheduled)
                await scheduled.DispatchScheduledAsync(update, ct,
                    (work, token) => ScheduleAsync(update, work, token)).ConfigureAwait(false);
            else
                await ScheduleAsync(update, () => _dispatcher.DispatchAsync(update, ct), ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Update processing failed, updateId={UpdateId}", update.Id);
        }
    }

    internal async Task RunWithGlobalLimitAsync(Func<Task> work, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var sem = _dopSemaphore;
        if (sem is not null)
            await sem.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            ct.ThrowIfCancellationRequested();
            await work().ConfigureAwait(false);
        }
        finally
        {
            sem?.Release();
        }
    }

    private void Cleanup()
    {
        var now = UtcNow;
        lock (_gate)
        {
            if (_stopTask is not null) return;
            foreach (var (key, actor) in _actors.ToArray())
            {
                if (actor.PendingCount != 0 || now - actor.LastActivityUtc < IdleTimeout) continue;
                _actors.Remove(key);
                actor.Complete();
            }
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct)
    {
        Task stopping;
        lock (_gate)
        {
            // Run outside the lock: cancellation can execute application callbacks.
            _stopTask ??= Task.Run(StopCoreAsync);
            stopping = _stopTask;
        }
        return stopping.WaitAsync(ct);
    }

    private async Task StopCoreAsync()
    {
        await _cleanup.DisposeAsync().ConfigureAwait(false);
        try
        {
            await _shutdown.CancelAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "An update cancellation callback failed during shutdown.");
        }
        Task[] pending;
        lock (_gate)
        {
            foreach (var actor in _actors.Values) actor.Complete();
            _received.Writer.TryComplete();
            pending = _active.Concat(_actors.Values.Select(actor => actor.Completion))
                .Append(_receiverWorker).ToArray();
        }
        await Task.WhenAll(pending).ConfigureAwait(false);
        _dopSemaphore?.Dispose();
        _shutdown.Dispose();
    }

    public void Dispose() => StopAsync(CancellationToken.None).GetAwaiter().GetResult();

    public ValueTask DisposeAsync() => new(StopAsync(CancellationToken.None));

    private static ActorKey? TryGetActorKey(Update upd)
    {
        var msg = upd.Message ?? upd.EditedMessage ?? upd.ChannelPost ?? upd.EditedChannelPost;
        if (msg is not null)
            return new ActorKey(ActorKeyKind.Chat, msg.Chat.Id);

        if (upd.CallbackQuery is not null)
        {
            // Key callback queries by user to avoid concurrent callback handling for the same user.
            // Keying by chat can deadlock request/response flows where the next user message is needed.
            var uid = upd.CallbackQuery.From?.Id ?? 0;
            if (uid != 0)
                return new ActorKey(ActorKeyKind.User, uid);

            return null;
        }

        if (upd.InlineQuery is not null)
            return new ActorKey(ActorKeyKind.User, upd.InlineQuery.From.Id);

        if (upd.ChosenInlineResult is not null)
            return new ActorKey(ActorKeyKind.User, upd.ChosenInlineResult.From.Id);

        if (upd.MyChatMember is not null)
            return new ActorKey(ActorKeyKind.Chat, upd.MyChatMember.Chat.Id);

        if (upd.ChatMember is not null)
            return new ActorKey(ActorKeyKind.Chat, upd.ChatMember.Chat.Id);

        if (upd.ChatJoinRequest is not null)
            return new ActorKey(ActorKeyKind.Chat, upd.ChatJoinRequest.Chat.Id);

        if (upd.Poll is not null)
            return null;

        var userId = upd.PreCheckoutQuery?.From?.Id
            ?? upd.ShippingQuery?.From?.Id;

        if (userId is not null && userId.Value != 0)
            return new ActorKey(ActorKeyKind.User, userId.Value);

        return null;
    }

}
