using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using TelegramBotKit.Sample.PhotoSelector.Data;
using TelegramBotKit.Sample.PhotoSelector.Data.Entities;

namespace TelegramBotKit.Sample.PhotoSelector.Bot;

public sealed class BotCommandMenuService(
    ITelegramBotClient botClient,
    IDbContextFactory<PhotoSelectorDbContext> contextFactory,
    ILogger<BotCommandMenuService> logger)
{
    private static readonly BotCommand[] GuestCommands =
    [
        new("start", "Открыть главное меню"),
        new("whoami", "Показать мой Telegram ID и доступ")
    ];

    private static readonly BotCommand[] UserCommands =
    [
        .. GuestCommands,
        new("browse", "Смотреть новые фото и видео"),
        new("liked", "Показать выбранные материалы"),
        new("skipped", "Показать пропущенные материалы"),
        new("all", "Показать все материалы"),
        new("sources", "Выбрать источник"),
        new("topics", "Выбрать тему"),
        new("export", "Скачать выбранные оригиналы")
    ];

    private static readonly BotCommand[] AdminCommands =
    [
        .. UserCommands,
        new("access_grant", "Выдать доступ по Telegram ID"),
        new("access_revoke", "Отозвать доступ по Telegram ID"),
        new("access_list", "Показать список пользователей")
    ];

    private static readonly BotCommand[] GroupAdminCommands =
    [
        new("source_enable", "Включить эту группу как источник"),
        new("source_disable", "Отключить эту группу как источник")
    ];

    public async Task ResetAndConfigureAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var users = await db.Users.AsNoTracking().ToListAsync(cancellationToken);

        await TryDeleteAsync(BotCommandScope.Default(), cancellationToken);
        await TryDeleteAsync(BotCommandScope.AllPrivateChats(), cancellationToken);
        await TryDeleteAsync(BotCommandScope.AllGroupChats(), cancellationToken);
        await TryDeleteAsync(BotCommandScope.AllChatAdministrators(), cancellationToken);

        foreach (var user in users)
            await TryDeleteAsync(BotCommandScope.Chat(user.TelegramUserId), cancellationToken);

        await TrySetAsync(GuestCommands, BotCommandScope.Default(), cancellationToken);
        await TrySetAsync(GuestCommands, BotCommandScope.AllPrivateChats(), cancellationToken);
        await TrySetAsync(GroupAdminCommands, BotCommandScope.AllChatAdministrators(), cancellationToken);

        foreach (var user in users.Where(x => x.IsEnabled))
            await ConfigureUserAsync(user.TelegramUserId, user.Role, cancellationToken);
    }

    public Task ConfigureUserAsync(
        long userId,
        UserRole role,
        CancellationToken cancellationToken = default)
        => TrySetAsync(
            role == UserRole.Admin ? AdminCommands : UserCommands,
            BotCommandScope.Chat(userId),
            cancellationToken);

    public Task RemoveUserCommandsAsync(long userId, CancellationToken cancellationToken = default)
        => TryDeleteAsync(BotCommandScope.Chat(userId), cancellationToken);

    private async Task TrySetAsync(
        IEnumerable<BotCommand> commands,
        BotCommandScope scope,
        CancellationToken cancellationToken)
    {
        try
        {
            await botClient.SetMyCommands(commands, scope, cancellationToken: cancellationToken);
        }
        catch (ApiRequestException exception)
        {
            logger.LogWarning(exception, "Не удалось обновить меню команд Telegram для области {ScopeType}", scope.Type);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Telegram недоступен при обновлении меню команд для области {ScopeType}", scope.Type);
        }
    }

    private async Task TryDeleteAsync(BotCommandScope scope, CancellationToken cancellationToken)
    {
        try
        {
            await botClient.DeleteMyCommands(scope, cancellationToken: cancellationToken);
        }
        catch (ApiRequestException exception)
        {
            logger.LogWarning(exception, "Не удалось очистить меню команд Telegram для области {ScopeType}", scope.Type);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Telegram недоступен при очистке меню команд для области {ScopeType}", scope.Type);
        }
    }
}
