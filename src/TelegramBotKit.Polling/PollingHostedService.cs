using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using TelegramBotKit.Options;
using TelegramBotKit.Hosting;

namespace TelegramBotKit.Polling;

internal sealed class PollingHostedService : BackgroundService
{
    private readonly ITelegramBotClient _bot;
    private readonly IUpdateReceiver _receiver;
    private readonly IOptions<TelegramBotKitOptions> _options;
    private readonly ILogger<PollingHostedService> _log;

    public PollingHostedService(
        ITelegramBotClient bot,
        IUpdateReceiver receiver,
        IOptions<TelegramBotKitOptions> options,
        ILogger<PollingHostedService> log)
    {
        _bot = bot;
        _receiver = receiver;
        _options = options;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        int? offset = null;
        var o = _options.Value;

        _log.LogInformation("TelegramBotKit polling started.");

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
                await _receiver.ReceiveAsync(upd, stoppingToken).ConfigureAwait(false);
                offset = upd.Id + 1;
            }
        }

        _log.LogInformation("TelegramBotKit polling stopped.");
    }

}
