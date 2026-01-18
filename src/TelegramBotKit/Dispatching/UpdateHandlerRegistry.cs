using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramBotKit.Fallbacks;

namespace TelegramBotKit.Dispatching;

internal sealed class UpdateHandlerRegistry
{
    private readonly Dictionary<UpdateType, Func<BotContext, Task>> _routes = new();
    private readonly object _sync = new();
    private volatile bool _isFrozen;

    public UpdateHandlerRegistry Map<TPayload>(UpdateType type, Func<Update, TPayload?> extractor)
        where TPayload : class
    {
        if (extractor is null) throw new ArgumentNullException(nameof(extractor));
        EnsureNotFrozen();

        lock (_sync)
        {
            // Hot-path optimized route:
            // - Avoid allocating an async state machine when there are 0 or 1 handlers.
            // - Prefer array iteration to avoid IEnumerator allocations when DI returns T[] (default MS.DI behavior).
            _routes[type] = ctx =>
            {
                var payload = extractor(ctx.Update);
                if (payload is null)
                    return Task.CompletedTask;

                var handlers = ctx.Services.GetServices<IUpdatePayloadHandler<TPayload>>();

                if (handlers is IUpdatePayloadHandler<TPayload>[] arr)
                {
                    var len = arr.Length;

                    if (len == 0)
                        return InvokeFallbackAsync(ctx);

                    if (len == 1)
                        return arr[0].HandleAsync(payload, ctx);

                    return InvokeManyAsync(arr, payload, ctx);
                }

                return InvokeEnumerableAsync(handlers, payload, ctx);
            };
        }

        return this;
    }

    private static Task InvokeFallbackAsync(BotContext ctx)
    {
        var fallback = ctx.Services.GetService<IDefaultUpdateHandler>();
        return fallback is null ? Task.CompletedTask : fallback.HandleAsync(ctx);
    }

    private static async Task InvokeManyAsync<TPayload>(
        IUpdatePayloadHandler<TPayload>[] handlers,
        TPayload payload,
        BotContext ctx)
        where TPayload : class
    {
        for (int i = 0; i < handlers.Length; i++)
            await handlers[i].HandleAsync(payload, ctx).ConfigureAwait(false);
    }

    private static async Task InvokeEnumerableAsync<TPayload>(
        IEnumerable<IUpdatePayloadHandler<TPayload>> handlers,
        TPayload payload,
        BotContext ctx)
        where TPayload : class
    {
        var any = false;
        foreach (var h in handlers)
        {
            any = true;
            await h.HandleAsync(payload, ctx).ConfigureAwait(false);
        }

        if (!any)
            await InvokeFallbackAsync(ctx).ConfigureAwait(false);
    }

    public bool TryGetRoute(UpdateType type, out Func<BotContext, Task> route)
        => _routes.TryGetValue(type, out route!);

    public void Freeze() => _isFrozen = true;

    private void EnsureNotFrozen()
    {
        if (_isFrozen)
            throw new InvalidOperationException("UpdateHandlerRegistry is frozen. Configure it before bot starts.");
    }
}
