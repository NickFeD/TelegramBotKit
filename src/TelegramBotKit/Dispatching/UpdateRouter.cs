using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramBotKit.Conversations;
using TelegramBotKit.Messaging;
using TelegramBotKit.Middleware;

namespace TelegramBotKit.Dispatching;

internal sealed class UpdateRouter : IUpdateDispatcher, IScheduledUpdateDispatcher
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITelegramBotClient _botClient;
    private readonly IMessageSender _sender;
    private readonly UpdateHandlerRegistry _registry;
    private readonly ILogger<UpdateRouter> _log;
    private readonly WaitForUserResponse _wait;
    private readonly MiddlewarePipeline _pipeline;

    private readonly BotContextDelegate _app;

    public UpdateRouter(
        IServiceScopeFactory scopeFactory,
        ITelegramBotClient botClient,
        IMessageSender sender,
        MiddlewarePipeline pipeline,
        UpdateHandlerRegistry registry,
        WaitForUserResponse wait,
        ILogger<UpdateRouter> log)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _botClient = botClient ?? throw new ArgumentNullException(nameof(botClient));
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _wait = wait ?? throw new ArgumentNullException(nameof(wait));
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));

        _app = _pipeline.Build(TerminalAsync);
    }

    private Task TerminalAsync(BotContext ctx)
    {
        if (TryPublishResponse(ctx))
            return Task.CompletedTask;

        var updateType = ctx.Update.Type;

        if (_registry.TryGetRoute(updateType, out var route))
            return route(ctx);

        _log.LogDebug("No handlers registered for UpdateType: {UpdateType}", updateType);
        return UpdateHandlerRegistry.FallbackAsync(ctx);
    }

    private bool OwnsConversation(Update update) =>
        update.Type == UpdateType.Message && _registry.UsesDefaultMessageHandler;

    private bool TryPublishResponse(BotContext ctx)
    {
        if (!OwnsConversation(ctx.Update)) return false;
        ctx.CancellationToken.ThrowIfCancellationRequested();
        return _wait.TryPublish(ctx.Update.Message!);
    }

    public Task DispatchScheduledAsync(Update update, CancellationToken ct,
        Func<Func<Task>, CancellationToken, Task> schedule)
    {
        ArgumentNullException.ThrowIfNull(update);
        ArgumentNullException.ThrowIfNull(schedule);
        if (!OwnsConversation(update) || !_wait.IsWaitingFor(update.Message!))
            return schedule(() => DispatchAsync(update, ct), ct);

        // A waiting command may own both the actor and the last parallelism slot.
        // Run the response through global middleware without taking either resource.
        // A canceled/consumed waiter must fall back to scheduled routing, never to
        // unscheduled command execution or a second invocation of middleware.
        var app = _pipeline.Build(ctx => TryPublishResponse(ctx)
            ? Task.CompletedTask
            : schedule(() => TerminalAsync(ctx), ctx.CancellationToken));
        return DispatchCoreAsync(update, ct, app);
    }

    public Task DispatchAsync(Update update, CancellationToken ct = default)
        => DispatchCoreAsync(update, ct, _app);

    private async Task DispatchCoreAsync(Update update, CancellationToken ct, BotContextDelegate app)
    {
        if (update is null) throw new ArgumentNullException(nameof(update));

        await using var scope = _scopeFactory.CreateAsyncScope();
        var ctx = new BotContext(update, _botClient, _sender, scope.ServiceProvider, ct);

        await app(ctx).ConfigureAwait(false);
    }
}
