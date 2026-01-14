namespace TelegramBotKit.Fallbacks;


internal sealed class NoopDefaultUpdateHandler : IDefaultUpdateHandler
{
    public Task HandleAsync(BotContext ctx) => Task.CompletedTask;
}
