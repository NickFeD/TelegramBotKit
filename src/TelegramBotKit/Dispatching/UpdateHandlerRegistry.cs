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
            _routes[type] = async ctx =>
            {
                var payload = extractor(ctx.Update);
                if (payload is null) return;

                var any = false;
                foreach (var h in ctx.Services.GetServices<IUpdatePayloadHandler<TPayload>>())
                {
                    any = true;
                    await h.HandleAsync(payload, ctx).ConfigureAwait(false);
                }

                if (!any)
                {
                    var fallback = ctx.Services.GetService<IDefaultUpdateHandler>();
                    if (fallback is not null)
                        await fallback.HandleAsync(ctx).ConfigureAwait(false);
                }
            };
        }

        return this;
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
