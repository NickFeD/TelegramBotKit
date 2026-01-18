using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.Threading.Channels;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramBotKit.Messaging;

internal sealed class QueuedMessageSender : IMessageSender, IAsyncDisposable
{
    private readonly MessageSender _inner;
    private readonly QueuedMessageSenderOptions _opt;
    private readonly ILogger<QueuedMessageSender> _log;

    private readonly Channel<OutgoingItem> _queue;
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _worker;

    private readonly long _globalMinIntervalTicks;
    private long _globalNextAllowedUtcTicks;

    private readonly long _perChatMinDelayTicks;
    private readonly Dictionary<long, long> _chatNextAllowedUtcTicks = new();

    public QueuedMessageSender(
        MessageSender inner,
        IOptions<QueuedMessageSenderOptions> options,
        ILogger<QueuedMessageSender> log)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _opt = (options ?? throw new ArgumentNullException(nameof(options))).Value ?? new QueuedMessageSenderOptions();
        _log = log ?? throw new ArgumentNullException(nameof(log));

        if (_opt.MaxQueueSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(_opt.MaxQueueSize), "MaxQueueSize must be > 0.");

        _queue = Channel.CreateBounded<OutgoingItem>(new BoundedChannelOptions(_opt.MaxQueueSize)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });

        // Precompute throttling ticks for the hot path.
        if (_opt.GlobalMaxPerSecond > 0)
        {
            var minIntervalTicks = (long)Math.Ceiling(TimeSpan.TicksPerSecond / (double)_opt.GlobalMaxPerSecond);
            _globalMinIntervalTicks = Math.Max(1, minIntervalTicks);
        }
        else
        {
            _globalMinIntervalTicks = 0;
        }

        _perChatMinDelayTicks = _opt.PerChatMinDelay > TimeSpan.Zero ? _opt.PerChatMinDelay.Ticks : 0;

        _worker = Task.Run(WorkerAsync, CancellationToken.None);
    }

    public Task<Message> SendText(long chatId, SendText msg, CancellationToken ct = default)
        => Enqueue(chatId, t => _inner.SendText(chatId, msg, t), ct);

    public Task<Message> ReplyText(Message replyTo, SendText msg, CancellationToken ct = default)
        => Enqueue(replyTo.Chat.Id, t => _inner.ReplyText(replyTo, msg, t), ct);

    public Task<Message> SendPhoto(long chatId, SendPhoto msg, CancellationToken ct = default)
        => Enqueue(chatId, t => _inner.SendPhoto(chatId, msg, t), ct);

    public Task<Message> ReplyPhoto(Message replyTo, SendPhoto msg, CancellationToken ct = default)
        => Enqueue(replyTo.Chat.Id, t => _inner.ReplyPhoto(replyTo, msg, t), ct);

    public Task<Message> EditText(long chatId, int messageId, EditText edit, CancellationToken ct = default)
        => Enqueue(chatId, t => _inner.EditText(chatId, messageId, edit, t), ct);

    public Task EditReplyMarkup(long chatId, int messageId, InlineKeyboardMarkup? keyboard, CancellationToken ct = default)
        => Enqueue(chatId, t => _inner.EditReplyMarkup(chatId, messageId, keyboard, t), ct);

    public Task EditPhoto(long chatId, int messageId, EditPhoto edit, CancellationToken ct = default)
        => Enqueue(chatId, t => _inner.EditPhoto(chatId, messageId, edit, t), ct);


    public Task AnswerCallback(string callbackQueryId, AnswerCallback answer, CancellationToken ct = default)
        => EnqueueNoChat(t => _inner.AnswerCallback(callbackQueryId, answer, t), ct);
    private Task Enqueue(long chatId, Func<CancellationToken, Task> op, CancellationToken ct)
    {
        if (chatId == 0) throw new ArgumentOutOfRangeException(nameof(chatId));
        return EnqueueCore(chatId, op, ct);
    }

    private Task EnqueueNoChat(Func<CancellationToken, Task> op, CancellationToken ct)
        => EnqueueCore(null, op, ct);

    private Task EnqueueCore(long? chatId, Func<CancellationToken, Task> op, CancellationToken ct)
    {
        if (op is null) throw new ArgumentNullException(nameof(op));

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        if (ct.IsCancellationRequested)
        {
            tcs.TrySetCanceled(ct);
            return tcs.Task;
        }

        var item = new OutgoingItem(
            ChatId: chatId,
            CallerToken: ct,
            RunOnceAsync: async token =>
            {
                await op(token).ConfigureAwait(false);
                tcs.TrySetResult();
            },
            OnCanceled: () => tcs.TrySetCanceled(ct),
            OnError: ex => tcs.TrySetException(ex));

        _ = TryWriteOrEnqueueAsync(item, ct);
        return tcs.Task;
    }


    private Task<T> Enqueue<T>(long chatId, Func<CancellationToken, Task<T>> op, CancellationToken ct)
    {
        if (chatId == 0) throw new ArgumentOutOfRangeException(nameof(chatId));
        return EnqueueCore(chatId, op, ct);
    }

    private Task<T> EnqueueNoChat<T>(Func<CancellationToken, Task<T>> op, CancellationToken ct)
        => EnqueueCore(null, op, ct);

    private Task<T> EnqueueCore<T>(long? chatId, Func<CancellationToken, Task<T>> op, CancellationToken ct)
    {
        if (op is null) throw new ArgumentNullException(nameof(op));

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        if (ct.IsCancellationRequested)
        {
            tcs.TrySetCanceled(ct);
            return tcs.Task;
        }

        var item = new OutgoingItem(
            ChatId: chatId,
            CallerToken: ct,
            RunOnceAsync: async token =>
            {
                var res = await op(token).ConfigureAwait(false);
                tcs.TrySetResult(res);
            },
            OnCanceled: () => tcs.TrySetCanceled(ct),
            OnError: ex => tcs.TrySetException(ex));

        _ = TryWriteOrEnqueueAsync(item, ct);
        return tcs.Task;
    }

    private Task TryWriteOrEnqueueAsync(OutgoingItem item, CancellationToken callerToken)
    {
        // Fast path: queue has space.
        if (_queue.Writer.TryWrite(item))
            return Task.CompletedTask;

        return EnqueueSlowAsync(item, callerToken);
    }

    private async Task EnqueueSlowAsync(OutgoingItem item, CancellationToken callerToken)
    {
        try
        {
            // If the channel is completed (e.g. during shutdown), this will fail quickly.
            await _queue.Writer.WriteAsync(item, callerToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            item.OnCanceled();
        }
        catch (ChannelClosedException)
        {
            // Treat shutdown/closed queue as cancellation.
            item.OnCanceled();
        }
        catch (Exception ex)
        {
            item.OnError(ex);
        }
    }

    private async Task WorkerAsync()
    {
        try
        {
            await foreach (var item in _queue.Reader.ReadAllAsync(_stop.Token).ConfigureAwait(false))
            {
                await ProcessItemAsync(item).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "QueuedMessageSender worker crashed.");
        }
    }

    private async Task ProcessItemAsync(OutgoingItem item)
    {
        CancellationTokenSource? linked = null;
        var ct = GetLinkedTokenOrSingle(item.CallerToken, _stop.Token, ref linked);

        try
        {
            for (var attempt = 1; attempt <= Math.Max(1, _opt.MaxRetryAttempts); attempt++)
            {
                try
                {
                    ct.ThrowIfCancellationRequested();

                    await ApplyThrottlesAsync(item.ChatId, ct).ConfigureAwait(false);

                    await item.RunOnceAsync(ct).ConfigureAwait(false);
                    return;
                }
                catch (OperationCanceledException) when (item.CallerToken.IsCancellationRequested || _stop.IsCancellationRequested)
                {
                    item.OnCanceled();
                    return;
                }
                catch (Exception ex) when (attempt < _opt.MaxRetryAttempts && TryGetRetryDelay(ex, attempt, out var delay))
                {
                    _log.LogWarning(ex, "Telegram send failed, retry in {Delay} (attempt {Attempt}/{Max}).", delay, attempt, _opt.MaxRetryAttempts);

                    try
                    {
                        await Task.Delay(delay, ct).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        item.OnCanceled();
                        return;
                    }
                }
                catch (Exception ex)
                {
                    item.OnError(ex);
                    return;
                }
            }
        }
        finally
        {
            linked?.Dispose();
        }
    }

    private async Task ApplyThrottlesAsync(long? chatId, CancellationToken ct)
    {
        if (_globalMinIntervalTicks > 0)
        {
            var now = DateTimeOffset.UtcNow.UtcTicks;

            if (now < _globalNextAllowedUtcTicks)
            {
                await Task.Delay(TimeSpan.FromTicks(_globalNextAllowedUtcTicks - now), ct).ConfigureAwait(false);
                now = DateTimeOffset.UtcNow.UtcTicks;
            }

            _globalNextAllowedUtcTicks = now + _globalMinIntervalTicks;
        }

        if (chatId is not null && _perChatMinDelayTicks > 0)
        {
            var now = DateTimeOffset.UtcNow.UtcTicks;

            if (!_chatNextAllowedUtcTicks.TryGetValue(chatId.Value, out var next))
                next = 0;

            if (now < next)
            {
                await Task.Delay(TimeSpan.FromTicks(next - now), ct).ConfigureAwait(false);
                now = DateTimeOffset.UtcNow.UtcTicks;
            }

            _chatNextAllowedUtcTicks[chatId.Value] = now + _perChatMinDelayTicks;
        }
    }

    private static CancellationToken GetLinkedTokenOrSingle(
        CancellationToken a,
        CancellationToken b,
        ref CancellationTokenSource? linked)
    {
        if (!a.CanBeCanceled) return b;
        if (!b.CanBeCanceled) return a;
        if (a == b) return a;

        linked = CancellationTokenSource.CreateLinkedTokenSource(a, b);
        return linked.Token;
    }


    private bool TryGetRetryDelay(Exception ex, int attempt, out TimeSpan delay)
    {
        delay = default;

        if (ex is ApiRequestException api && api.ErrorCode == 429)
        {
            var retryAfter = TryGetRetryAfterSeconds(api);
            delay = retryAfter is not null && retryAfter.Value > 0
                ? TimeSpan.FromSeconds(retryAfter.Value)
                : _opt.DefaultRetryDelay;

            return delay > TimeSpan.Zero;
        }

        if (ex is ApiRequestException api5 && api5.ErrorCode >= 500 && api5.ErrorCode < 600)
        {
            delay = CalculateBackoff(attempt);
            return delay > TimeSpan.Zero;
        }

        if (ex is HttpRequestException)
        {
            delay = CalculateBackoff(attempt);
            return delay > TimeSpan.Zero;
        }

        return false;
    }

    private TimeSpan CalculateBackoff(int attempt)
    {
        var pow = Math.Clamp(attempt - 1, 0, 10);
        var factor = 1 << pow;

        var ticks = _opt.ServerErrorBaseDelay.Ticks * factor;
        if (ticks <= 0) ticks = _opt.ServerErrorBaseDelay.Ticks;

        var backoff = new TimeSpan(Math.Min(ticks, _opt.ServerErrorMaxDelay.Ticks));
        return backoff;
    }

    private static int? TryGetRetryAfterSeconds(ApiRequestException ex)
    {
        try
        {
            var p = ex.Parameters;
            if (p is null) return null;

            var prop = p.GetType().GetProperty("RetryAfter");
            if (prop is null) return null;

            var v = prop.GetValue(p);
            return v switch
            {
                int i => i,
                long l => (int)l,
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            _stop.Cancel();
            _queue.Writer.TryComplete();
        }
        catch
        {
        }

        try
        {
            await _worker.ConfigureAwait(false);
        }
        catch
        {
        }

        _stop.Dispose();
    }

    internal sealed record OutgoingItem(
        long? ChatId,
        CancellationToken CallerToken,
        Func<CancellationToken, Task> RunOnceAsync,
        Action OnCanceled,
        Action<Exception> OnError);
}
