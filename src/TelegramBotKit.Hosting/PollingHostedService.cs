using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using TelegramBotKit.Conversations;
using TelegramBotKit.Dispatching;
using TelegramBotKit.Options;

namespace TelegramBotKit.Hosting;

public sealed class PollingHostedService : BackgroundService
{
    private readonly ITelegramBotClient _bot;
    private readonly UpdateRouter _router;
    private readonly WaitForUserResponse _wait;
    private readonly IOptions<TelegramBotKitOptions> _options;
    private readonly ILogger<PollingHostedService> _log;

    public PollingHostedService(
        ITelegramBotClient bot,
        UpdateRouter router,
        WaitForUserResponse wait,
        IOptions<TelegramBotKitOptions> options,
        ILogger<PollingHostedService> log)
    {
        _bot = bot;
        _router = router;
        _wait = wait;
        _options = options;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        int? offset = null;
        var o = _options.Value;

        _log.LogInformation("TelegramBotKit polling started (no limits).");

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

                // Fire-and-forget обработка без лимитов
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _router.RouteAsync(upd, stoppingToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        // ignore
                    }
                    catch (Exception ex)
                    {
                        _log.LogError(ex, "Update processing failed, updateId={UpdateId}", upd.Id);
                    }
                }, CancellationToken.None);
            }
        }

        _log.LogInformation("TelegramBotKit polling stopped.");
    }
}
