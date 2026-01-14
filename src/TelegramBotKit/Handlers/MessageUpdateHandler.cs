using Telegram.Bot.Types;
using TelegramBotKit.Conversations;
using TelegramBotKit.Dispatching;
using TelegramBotKit.Fallbacks;
using TelegramBotKit.Routing;

namespace TelegramBotKit.Handlers;

/// <summary>
/// Обрабатывает UpdateType.Message (payload Message) и маршрутизирует в команды.
/// </summary>
internal sealed class MessageUpdateHandler : IUpdatePayloadHandler<Message>
{
    private readonly CommandRouter _router;
    private readonly WaitForUserResponse _wait;
    private readonly IDefaultMessageHandler _defaultMessage;

    public MessageUpdateHandler(
        CommandRouter router,
        WaitForUserResponse wait,
        IDefaultMessageHandler defaultMessage)
    {
        _router = router ?? throw new ArgumentNullException(nameof(router));
        _wait = wait ?? throw new ArgumentNullException(nameof(wait));
        _defaultMessage = defaultMessage ?? throw new ArgumentNullException(nameof(defaultMessage));
    }

    public async Task HandleAsync(Message payload, BotContext ctx)
    {
        ctx.CancellationToken.ThrowIfCancellationRequested();

        // 1) Сначала пробуем доставить в ожидание ответа (если оно включено на чат/пользователя)
        if (_wait.TryPublish(payload))
            return;

        // 2) Пытаемся сматчить команды
        var handled = await _router.TryRouteMessageAsync(payload, ctx).ConfigureAwait(false);
        if (handled)
            return;

        // 3) Иначе — default
        await _defaultMessage.HandleAsync(payload, ctx).ConfigureAwait(false);
    }
}
