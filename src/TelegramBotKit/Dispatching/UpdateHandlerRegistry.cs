using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramBotKit.Fallbacks;

namespace TelegramBotKit.Dispatching;

/// <summary>
/// Реестр: UpdateType -> (extract payload + вызвать все обработчики этого payload).
/// Позволяет убрать монолитный switch и расширять поддержку новых UpdateType.
/// </summary>
internal sealed class UpdateHandlerRegistry
{
    // Минимальная сигнатура маршрута: BotContext уже содержит Services и CancellationToken.
    private readonly Dictionary<UpdateType, Func<BotContext, Task>> _routes = new();
    private readonly object _sync = new();
    private volatile bool _isFrozen;

    /// <summary>
    /// Зарегистрировать маршрут для конкретного UpdateType:
    /// - extractor достаёт payload из Update
    /// - дальше вызываются все IUpdatePayloadHandler&lt;TPayload&gt; из DI.
    /// </summary>
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

                // Если обработчиков для payload-типа не зарегистрировано —
                // даём шанс глобальному fallback-обработчику Update.
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

    /// <summary>
    /// Пытаемся получить маршрут обработки UpdateType.
    /// </summary>
    public bool TryGetRoute(UpdateType type, out Func<BotContext, Task> route)
        => _routes.TryGetValue(type, out route!);

    /// <summary>
    /// Заморозить реестр: после этого Map(...) нельзя вызывать.
    /// </summary>
    public void Freeze() => _isFrozen = true;

    private void EnsureNotFrozen()
    {
        if (_isFrozen)
            throw new InvalidOperationException("UpdateHandlerRegistry is frozen. Configure it before bot starts.");
    }
}
