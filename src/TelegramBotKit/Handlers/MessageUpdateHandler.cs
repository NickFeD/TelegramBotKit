using Telegram.Bot.Types;
using TelegramBotKit.Conversations;
using TelegramBotKit.Dispatching;
using TelegramBotKit.Routing;

namespace TelegramBotKit.Handlers;

/// <summary>
/// Обрабатывает UpdateType.Message (payload Message) и маршрутизирует в команды.
/// </summary>
public sealed class MessageUpdateHandler : IUpdatePayloadHandler<Message>
{
    private readonly CommandRouter _router;
    private readonly WaitForUserResponse _wait;

    public MessageUpdateHandler(CommandRouter router, WaitForUserResponse wait)
    {
        _router = router ?? throw new ArgumentNullException(nameof(router));
        _wait = wait ?? throw new ArgumentNullException(nameof(wait));
    }

    public Task HandleAsync(Message payload, BotContext ctx, CancellationToken ct)
    {
        // Сначала пробуем доставить в ожидание ответа
        if (_wait.TryPublish(payload))
            return Task.CompletedTask;

        // Если ожидания нет — обычный роутинг команд
        return _router.RouteMessageAsync(payload, ctx, ct);
    }
}
