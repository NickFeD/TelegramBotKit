using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramBotKit.Dispatching;
using TelegramBotKit.Options;

namespace TelegramBotKit.Hosting;

internal sealed class UpdateActorScheduler : IDisposable
{
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan CleanupPeriod = TimeSpan.FromMinutes(1);

    private readonly IUpdateDispatcher _dispatcher;
    private readonly ILogger<UpdateActorScheduler> _log;
    private readonly SemaphoreSlim? _dopSemaphore;

    private readonly ConcurrentDictionary<ActorKey, Actor> _actors = new();
    private readonly Timer _cleanup;

    public IUpdateDispatcher Dispatcher => _dispatcher;
    public ILogger<UpdateActorScheduler> Log => _log;

    public UpdateActorScheduler(
        IUpdateDispatcher dispatcher,
        IOptions<TelegramBotKitOptions> options,
        ILogger<UpdateActorScheduler> log)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _log = log ?? throw new ArgumentNullException(nameof(log));

        var dop = (options ?? throw new ArgumentNullException(nameof(options))).Value.Polling.MaxDegreeOfParallelism;
        _dopSemaphore = dop > 0 ? new SemaphoreSlim(dop, dop) : null;

        _cleanup = new Timer(_ => Cleanup(), null, CleanupPeriod, CleanupPeriod);
    }

    public ValueTask EnqueueAsync(Update update, CancellationToken ct)
    {
        if (update is null) throw new ArgumentNullException(nameof(update));

        var key = TryGetActorKey(update);

        if (key is null)
        {
            _ = ProcessUnkeyedAsync(update, ct);
            return ValueTask.CompletedTask;
        }

        var actor = _actors.GetOrAdd(key.Value, k => new Actor(k, this));
        return actor.EnqueueAsync(update, ct);
    }

    private async Task ProcessUnkeyedAsync(Update update, CancellationToken ct)
    {
        try
        {
            await RunWithGlobalLimitAsync(() => _dispatcher.DispatchAsync(update, ct), ct).ConfigureAwait(false);
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
        var sem = _dopSemaphore;
        if (sem is not null)
            await sem.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            await work().ConfigureAwait(false);
        }
        finally
        {
            sem?.Release();
        }
    }

    private void Cleanup()
    {
        var now = DateTime.UtcNow;

        foreach (var kv in _actors)
        {
            var actor = kv.Value;

            if (actor.PendingCount != 0)
                continue;

            if (now - actor.LastActivityUtc < IdleTimeout)
                continue;

            if (_actors.TryRemove(kv.Key, out var removed) && ReferenceEquals(removed, actor))
            {
                removed.Complete();
            }
        }
    }

    public void Dispose()
    {
        _cleanup.Dispose();

        foreach (var kv in _actors)
            kv.Value.Complete();

        _dopSemaphore?.Dispose();
    }

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