using Telegram.Bot.Types;

namespace TelegramBotKit.Fallbacks;

internal sealed class NoopDefaultMessageHandler : IDefaultMessageHandler
{
    public Task HandleAsync(Message message, BotContext ctx) => Task.CompletedTask;
}
