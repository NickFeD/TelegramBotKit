using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using TelegramBotKit.Conversations;
using TelegramBotKit.Options;

namespace TelegramBotKit.Hosting;

internal sealed class PollingHostedService : BackgroundService
{
    private readonly ITelegramBotClient _bot;
    private readonly UpdateActorScheduler _scheduler;
    private readonly WaitForUserResponse _wait;
    private readonly IOptions<TelegramBotKitOptions> _options;
    private readonly ILogger<PollingHostedService> _log;

    public PollingHostedService(
        ITelegramBotClient bot,
        UpdateActorScheduler scheduler,
        WaitForUserResponse wait,
        IOptions<TelegramBotKitOptions> options,
        ILogger<PollingHostedService> log)
    {
        _bot = bot;
        _scheduler = scheduler;
        _wait = wait;
        _options = options;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        int? offset = null;
        var o = _options.Value;

        _log.LogInformation(
            "TelegramBotKit polling started (MaxDegreeOfParallelism={Dop}).",
            o.Polling.MaxDegreeOfParallelism);

        while (!stoppingToken.IsCancellationRequested)
        {
            IReadOnlyList<Update> updates;

            try
            {
                updates = await _bot.GetUpdates(
                    offset: offset,
                    limit: o.Polling.Limit,
                    timeout: o.Polling.TimeoutSeconds,
                    allowedUpdates: o.Polling.AllowedUpdates,
                    cancellationToken: stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (ApiRequestException ex)
            {
                _log.LogError(ex, "GetUpdates API error. Retry in 1s.");
                await Task.Delay(1000, stoppingToken).ConfigureAwait(false);
                continue;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "GetUpdates failed. Retry in 1s.");
                await Task.Delay(1000, stoppingToken).ConfigureAwait(false);
                continue;
            }

            if (updates.Count == 0)
                continue;

            foreach (var upd in updates)
            {
                // ВАЖНО: offset двигаем сразу, чтобы не получать один и тот же update снова.
                offset = upd.Id + 1;

                // Fast-path: если это ответ ожидающему пользователю — "скармливаем" ожиданию
                // и НЕ отправляем в обычную обработку (чтобы команды не мешали диалогу).
                if (upd.Message is not null && _wait.TryPublish(upd.Message))
                    continue;

                // Actor-per-chat/user dispatch:
                // - Message-like updates are ordered by ChatId
                // - Inline flows are ordered by UserId
                // - CallbackQuery is processed without ordering (buttons)
                await _scheduler.EnqueueAsync(upd, stoppingToken).ConfigureAwait(false);
            }
        }

        _log.LogInformation("TelegramBotKit polling stopped.");
    }
}
