using Telegram.Bot.Types;

namespace TelegramBotKit.Fallbacks;

internal sealed class NoopDefaultCallbackHandler : IDefaultCallbackHandler
{
    public Task HandleAsync(CallbackQuery query, BotContext ctx)
        => Task.CompletedTask;
}
