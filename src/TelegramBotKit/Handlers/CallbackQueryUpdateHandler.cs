using Telegram.Bot.Types;
using TelegramBotKit.Dispatching;
using TelegramBotKit.Fallbacks;
using TelegramBotKit.Routing;

namespace TelegramBotKit.Handlers;

/// <summary>
/// Обрабатывает UpdateType.CallbackQuery (payload CallbackQuery) и маршрутизирует в команды.
/// </summary>
internal sealed class CallbackQueryUpdateHandler : IUpdatePayloadHandler<CallbackQuery>
{
    private readonly CommandRouter _router;
    private readonly IDefaultCallbackHandler _defaultCallback;

    public CallbackQueryUpdateHandler(CommandRouter router, IDefaultCallbackHandler defaultCallback)
    {
        _router = router ?? throw new ArgumentNullException(nameof(router));
        _defaultCallback = defaultCallback ?? throw new ArgumentNullException(nameof(defaultCallback));
    }

    public async Task HandleAsync(CallbackQuery payload, BotContext ctx)
    {
        ctx.CancellationToken.ThrowIfCancellationRequested();

        var handled = await _router.TryRouteCallbackAsync(payload, ctx).ConfigureAwait(false);
        if (handled)
            return;

        await _defaultCallback.HandleAsync(payload, ctx).ConfigureAwait(false);
    }
}
