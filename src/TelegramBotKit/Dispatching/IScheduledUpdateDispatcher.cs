using Telegram.Bot.Types;

namespace TelegramBotKit.Dispatching;

// Hosting supplies execution ordering; core retains routing/conversation policy.
internal interface IScheduledUpdateDispatcher
{
    Task DispatchScheduledAsync(Update update, CancellationToken ct,
        Func<Func<Task>, CancellationToken, Task> schedule);
}
