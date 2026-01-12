using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramBotKit.Middleware;

namespace TelegramBotKit.Dispatching;

/// <summary>
/// Главная точка входа для обработки Update:
/// scope-per-update -> BotContext -> middleware -> dispatch по UpdateType.
/// </summary>
public sealed class UpdateRouter
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITelegramBotClient _botClient;
    private readonly MiddlewarePipeline _pipeline;
    private readonly UpdateHandlerRegistry _registry;
    private readonly ILogger<UpdateRouter> _log;

    public UpdateRouter(
        IServiceScopeFactory scopeFactory,
        ITelegramBotClient botClient,
        MiddlewarePipeline pipeline,
        UpdateHandlerRegistry registry,
        ILogger<UpdateRouter> log)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _botClient = botClient ?? throw new ArgumentNullException(nameof(botClient));
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public async Task RouteAsync(Update update, CancellationToken ct)
    {
        if (update is null) throw new ArgumentNullException(nameof(update));

        using var scope = _scopeFactory.CreateScope();
        var ctx = new BotContext(update, _botClient, scope.ServiceProvider, ct);

        // terminal: что произойдёт в конце цепочки middleware
        Task Terminal()
        {
            if (_registry.TryGetRoute(update.Type, out var route))
                return route(scope.ServiceProvider, ctx, ct);

            _log.LogDebug("No handlers registered for UpdateType: {UpdateType}", update.Type);
            return Task.CompletedTask;
        }

        // Всегда прогоняем через pipeline (даже если тип не поддержан),
        // чтобы работали глобальные middleware (логирование/метрики/ошибки).
        await _pipeline.ExecuteAsync(ctx, Terminal).ConfigureAwait(false);
    }
}