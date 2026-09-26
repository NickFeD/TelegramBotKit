using Telegram.Bot.Types;
using TelegramBotKit.Fallbacks;

namespace TelegramBotKit.Sample.PhotoSelector.Bot;

public sealed class DefaultMessageHandler : IDefaultMessageHandler
{
    public Task HandleAsync(Message message, BotContext ctx) => Task.CompletedTask;
}
