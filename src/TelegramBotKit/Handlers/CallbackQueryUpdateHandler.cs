using Telegram.Bot.Types;
using TelegramBotKit.Dispatching;
using TelegramBotKit.Routing;

namespace TelegramBotKit.Handlers;

/// <summary>
/// Обрабатывает UpdateType.CallbackQuery (payload CallbackQuery) и маршрутизирует в команды.
/// </summary>
public sealed class CallbackQueryUpdateHandler : IUpdatePayloadHandler<CallbackQuery>
{
    private readonly CommandRouter _router;

    public CallbackQueryUpdateHandler(CommandRouter router)
        => _router = router ?? throw new ArgumentNullException(nameof(router));

    public Task HandleAsync(CallbackQuery payload, BotContext ctx, CancellationToken ct)
        => _router.RouteCallbackAsync(payload, ctx, ct);
}