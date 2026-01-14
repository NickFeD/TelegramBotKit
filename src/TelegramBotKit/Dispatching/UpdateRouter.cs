using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramBotKit.Fallbacks;
using TelegramBotKit.Messaging;
using TelegramBotKit.Middleware;

namespace TelegramBotKit.Dispatching;

internal sealed class UpdateRouter : IUpdateDispatcher
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITelegramBotClient _botClient;
    private readonly IMessageSender _sender;
    private readonly UpdateHandlerRegistry _registry;
    private readonly IDefaultUpdateHandler _defaultUpdate;
    private readonly ILogger<UpdateRouter> _log;

    private readonly BotContextDelegate _app;

    public UpdateRouter(
        IServiceScopeFactory scopeFactory,
        ITelegramBotClient botClient,
        IMessageSender sender,
        MiddlewarePipeline pipeline,
        UpdateHandlerRegistry registry,
        IDefaultUpdateHandler defaultUpdate,
        ILogger<UpdateRouter> log)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _botClient = botClient ?? throw new ArgumentNullException(nameof(botClient));
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _defaultUpdate = defaultUpdate ?? throw new ArgumentNullException(nameof(defaultUpdate));
        _log = log ?? throw new ArgumentNullException(nameof(log));

        _app = (pipeline ?? throw new ArgumentNullException(nameof(pipeline)))
            .Build(TerminalAsync);
    }

    private Task TerminalAsync(BotContext ctx)
    {
        var updateType = ctx.Update.Type;

        if (_registry.TryGetRoute(updateType, out var route))
            return route(ctx);

        _log.LogDebug("No handlers registered for UpdateType: {UpdateType}", updateType);
        return _defaultUpdate.HandleAsync(ctx);
    }

    public async Task DispatchAsync(Update update, CancellationToken ct = default)
    {
        if (update is null) throw new ArgumentNullException(nameof(update));

        using var scope = _scopeFactory.CreateScope();
        var ctx = new BotContext(update, _botClient, _sender, scope.ServiceProvider, ct);

        await _app(ctx).ConfigureAwait(false);
    }
}
