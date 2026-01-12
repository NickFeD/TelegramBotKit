using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace TelegramBotKit.Dispatching;

/// <summary>
/// Реестр: UpdateType -> (extract payload + вызвать все обработчики этого payload).
/// Позволяет убрать монолитный switch и расширять поддержку новых UpdateType.
/// </summary>
public sealed class UpdateHandlerRegistry
{
    private readonly Dictionary<UpdateType, Func<IServiceProvider, BotContext, CancellationToken, Task>> _routes = new();
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
            _routes[type] = async (sp, ctx, ct) =>
            {
                var payload = extractor(ctx.Update);
                if (payload is null) return;

                var handlers = sp.GetServices<IUpdatePayloadHandler<TPayload>>();
                foreach (var h in handlers)
                    await h.HandleAsync(payload, ctx, ct).ConfigureAwait(false);
            };
        }

        return this;
    }

    /// <summary>
    /// Пытаемся получить маршрут обработки UpdateType.
    /// </summary>
    public bool TryGetRoute(UpdateType type, out Func<IServiceProvider, BotContext, CancellationToken, Task> route)
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
