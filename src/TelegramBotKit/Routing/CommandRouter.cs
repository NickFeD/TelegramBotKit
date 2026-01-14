using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot.Types;
using TelegramBotKit.Commands;

namespace TelegramBotKit.Routing;

internal sealed class CommandRouter
{
    private readonly CommandRegistry _registry;
    private readonly IServiceProvider _services;

    public CommandRouter(CommandRegistry registry, IServiceProvider services)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    public async Task<bool> TryRouteMessageAsync(Message message, BotContext ctx)
    {
        ctx.CancellationToken.ThrowIfCancellationRequested();

        var text = message.Text;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        text = text.Trim();

        if (text.Length > 1 && text[0] == '/')
        {
            var cmd = ExtractSlashCommand(text);
            if (cmd is null)
                return false;

            if (!_registry.TryGetMessageCommand(cmd, out var commandType))
                return false;

            var handler = (IMessageCommand)_services.GetRequiredService(commandType);
            await handler.HandleAsync(message, ctx).ConfigureAwait(false);
            return true;
        }

        if (!_registry.TryGetTextCommand(text, out var textCommandType))
            return false;

        var bestHandler = (ITextCommand)_services.GetRequiredService(textCommandType);
        await bestHandler.HandleAsync(message, ctx).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> TryRouteCallbackAsync(CallbackQuery query, BotContext ctx)
    {
        ctx.CancellationToken.ThrowIfCancellationRequested();

        var data = query.Data;
        if (string.IsNullOrWhiteSpace(data))
            return false;

        if (!TryParseCallbackData(data, out var key, out var args))
            return false;

        if (!_registry.TryGetCallbackCommand(key, out var commandType))
            return false;

        var handler = (ICallbackCommand)_services.GetRequiredService(commandType);
        await handler.HandleAsync(query, args, ctx).ConfigureAwait(false);
        return true;
    }

    private static string? ExtractSlashCommand(string text)
    {
        var first = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries)[0];
        if (first.Length < 2 || first[0] != '/')
            return null;

        var at = first.IndexOf('@');
        if (at >= 0)
            first = first[..at];

        return first;
    }

    private static bool TryParseCallbackData(string data, out string key, out string[] args)
    {
        var parts = data.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            key = string.Empty;
            args = Array.Empty<string>();
            return false;
        }

        key = parts[0];
        args = parts.Length > 1 ? parts.Skip(1).ToArray() : Array.Empty<string>();
        return !string.IsNullOrWhiteSpace(key);
    }
}
