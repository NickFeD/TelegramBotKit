namespace TelegramBotKit.Fallbacks;

// -------------------- Default Noop implementations --------------------

internal sealed class NoopDefaultUpdateHandler : IDefaultUpdateHandler
{
    public Task HandleAsync(BotContext ctx, CancellationToken ct) => Task.CompletedTask;
}
