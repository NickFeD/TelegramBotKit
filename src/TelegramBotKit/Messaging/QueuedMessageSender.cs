using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
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

    private long _globalNextAllowedUtcTicks;
    private readonly ConcurrentDictionary<long, long> _chatNextAllowedUtcTicks = new();

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
        => EnqueueNoChat(async t =>
        {
            await _inner.AnswerCallback(callbackQueryId, answer, t).ConfigureAwait(false);
            return 0;
        }, ct);
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

        _ = WriteAsync(item);
        return tcs.Task;

        async Task WriteAsync(OutgoingItem it)
        {
            try
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _stop.Token);
                await _queue.Writer.WriteAsync(it, linked.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                it.OnCanceled();
            }
            catch (Exception ex)
            {
                it.OnError(ex);
            }
        }
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

        _ = WriteAsync(item);
        return tcs.Task;

        async Task WriteAsync(OutgoingItem it)
        {
            try
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _stop.Token);
                await _queue.Writer.WriteAsync(it, linked.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                it.OnCanceled();
            }
            catch (Exception ex)
            {
                it.OnError(ex);
            }
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
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token, item.CallerToken);
        var ct = linked.Token;

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

    private async Task ApplyThrottlesAsync(long? chatId, CancellationToken ct)
    {
        if (_opt.GlobalMaxPerSecond > 0)
        {
            var minIntervalTicks = (long)Math.Ceiling(TimeSpan.TicksPerSecond / (double)_opt.GlobalMaxPerSecond);
            if (minIntervalTicks < 1) minIntervalTicks = 1;

            var now = DateTimeOffset.UtcNow.UtcTicks;

            if (now < _globalNextAllowedUtcTicks)
            {
                await Task.Delay(TimeSpan.FromTicks(_globalNextAllowedUtcTicks - now), ct).ConfigureAwait(false);
                now = DateTimeOffset.UtcNow.UtcTicks;
            }

            _globalNextAllowedUtcTicks = now + minIntervalTicks;
        }

        if (chatId is not null && _opt.PerChatMinDelay > TimeSpan.Zero)
        {
            var now = DateTimeOffset.UtcNow.UtcTicks;
            var next = _chatNextAllowedUtcTicks.GetOrAdd(chatId.Value, 0);

            if (now < next)
            {
                await Task.Delay(TimeSpan.FromTicks(next - now), ct).ConfigureAwait(false);
                now = DateTimeOffset.UtcNow.UtcTicks;
            }

            _chatNextAllowedUtcTicks[chatId.Value] = now + _opt.PerChatMinDelay.Ticks;
        }
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
