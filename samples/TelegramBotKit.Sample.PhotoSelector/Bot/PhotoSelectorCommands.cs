using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramBotKit.Keyboards;
using TelegramBotKit.Messaging;
using TelegramBotKit.Sample.PhotoSelector.Access;
using TelegramBotKit.Sample.PhotoSelector.Browsing;
using TelegramBotKit.Sample.PhotoSelector.Data;
using TelegramBotKit.Sample.PhotoSelector.Data.Entities;
using TelegramBotKit.Sample.PhotoSelector.Media;

namespace TelegramBotKit.Sample.PhotoSelector.Bot;

public sealed class PhotoSelectorCommands(
    AccessService accessService,
    SourceChatService sourceChatService,
    BrowseService browseService,
    SelectionService selectionService,
    ExportService exportService,
    BotUiService ui,
    BotCommandMenuService commandMenu,
    IDbContextFactory<PhotoSelectorDbContext> contextFactory)
{
    public async Task StartAsync(Message message, BotContext ctx)
    {
        if (!TryGetPrivateHuman(message, out var userId))
        {
            await ReplyAsync(message, "Используйте эту команду в личном чате с ботом.", ctx);
            return;
        }

        var user = await accessService.FindEnabledAsync(userId, ctx.CancellationToken);
        if (user is null)
        {
            await ReplyAsync(message, "Доступ запрещён. Попросите администратора бота выдать вам доступ.", ctx);
            return;
        }

        await ctx.Sender.SendText(
            message.Chat.Id,
            new SendText
            {
                Text = user.Role == UserRole.Admin
                    ? "Бот выбора фото и видео готов. Вы вошли как администратор."
                    : "Бот выбора фото и видео готов.",
                ReplyMarkup = BotUiService.MainMenu(user.Role == UserRole.Admin)
            },
            ctx.CancellationToken);
    }

    public async Task WhoAmIAsync(Message message, BotContext ctx)
    {
        if (message.From is null)
            return;
        var user = await accessService.FindEnabledAsync(message.From.Id, ctx.CancellationToken);
        var status = user is null
            ? "нет доступа"
            : user.Role == UserRole.Admin ? "администратор" : "пользователь";
        await ReplyAsync(message, $"Telegram ID: {message.From.Id}\nДоступ: {status}", ctx);
    }

    public async Task GrantAsync(Message message, BotContext ctx)
    {
        if (!TryParseUserId(message.Text, "/access_grant", out var targetId))
        {
            await ReplyAsync(message, "Использование: /access_grant <telegram_user_id>", ctx);
            return;
        }

        await RunAdminActionAsync(message, ctx, async administratorId =>
        {
            await accessService.GrantAsync(administratorId, targetId, ctx.CancellationToken);
            await commandMenu.ConfigureUserAsync(targetId, UserRole.User, ctx.CancellationToken);
            return $"Доступ выдан пользователю {targetId}.";
        });
    }

    public async Task RevokeAsync(Message message, BotContext ctx)
    {
        if (!TryParseUserId(message.Text, "/access_revoke", out var targetId))
        {
            await ReplyAsync(message, "Использование: /access_revoke <telegram_user_id>", ctx);
            return;
        }

        await RunAdminActionAsync(message, ctx, async administratorId =>
        {
            await accessService.RevokeAsync(administratorId, targetId, ctx.CancellationToken);
            await commandMenu.RemoveUserCommandsAsync(targetId, ctx.CancellationToken);
            return $"Доступ пользователя {targetId} отозван.";
        });
    }

    public async Task ListAccessAsync(Message message, BotContext ctx)
    {
        await RunAdminActionAsync(message, ctx, async _ =>
        {
            var users = await accessService.ListAsync(ctx.CancellationToken);
            return users.Count == 0
                ? "Нет пользователей с доступом."
                : string.Join('\n', users.Select(x =>
                    $"{x.TelegramUserId}: " +
                    $"{(x.Role == UserRole.Admin ? "администратор" : "пользователь")}, " +
                    $"{(x.IsEnabled ? "доступ включён" : "доступ отключён")}"));
        });
    }

    public Task EnableSourceAsync(Message message, BotContext ctx)
        => SetSourceAsync(message, true, ctx);

    public Task DisableSourceAsync(Message message, BotContext ctx)
        => SetSourceAsync(message, false, ctx);

    public Task BrowseAsync(Message message, BotContext ctx)
        => StartBrowseFromMessageAsync(message, BrowseFilter.Unreviewed, ctx);

    public Task LikedAsync(Message message, BotContext ctx)
        => StartBrowseFromMessageAsync(message, BrowseFilter.Liked, ctx);

    public Task SkippedAsync(Message message, BotContext ctx)
        => StartBrowseFromMessageAsync(message, BrowseFilter.Skipped, ctx);

    public Task AllAsync(Message message, BotContext ctx)
        => StartBrowseFromMessageAsync(message, BrowseFilter.All, ctx);

    public async Task TopicsAsync(Message message, BotContext ctx)
    {
        if (!await RequirePrivateAuthorizedAsync(message, ctx))
            return;
        await SendTopicsAsync(message.From!.Id, message.Chat.Id, ctx);
    }

    public async Task SourcesAsync(Message message, BotContext ctx)
    {
        if (!await RequirePrivateAuthorizedAsync(message, ctx))
            return;
        await SendSourcesAsync(message.From!.Id, message.Chat.Id, ctx);
    }

    public async Task ExportAsync(Message message, BotContext ctx)
    {
        if (!await RequirePrivateAuthorizedAsync(message, ctx))
            return;
        await ExportAsync(message.From!.Id, message.Chat.Id, ctx);
    }

    public async Task HandleCallbackAsync(CallbackQuery query, string[] args, BotContext ctx)
    {
        if (query.Message is null || query.Message.Chat.Type != ChatType.Private || query.From.IsBot)
        {
            await AnswerAsync(query, "Это действие доступно только в личном чате с ботом.", true, ctx);
            return;
        }

        var userId = query.From.Id;
        var user = await accessService.FindEnabledAsync(userId, ctx.CancellationToken);
        if (user is null)
        {
            await AnswerAsync(query, "Доступ запрещён.", true, ctx);
            return;
        }

        var action = args.FirstOrDefault();
        long sourceChatId = 0;
        long topicChatId = 0;
        int threadId = 0;
        long mediaId = 0;

        switch (action)
        {
            case "menu":
            case "browse":
            case "liked":
            case "skipped":
            case "all":
            case "topics":
            case "sources":
            case "export":
                break;
            case "admin" when user.Role == UserRole.Admin:
                break;
            case "admin":
                await AnswerAsync(query, "Требуются права администратора.", true, ctx);
                return;
            case "source" when args.Length == 2 && long.TryParse(args[1], out sourceChatId):
                break;
            case "topic" when args.Length == 3 &&
                                   long.TryParse(args[1], out topicChatId) &&
                                   int.TryParse(args[2], out threadId):
                break;
            case "like":
            case "skip":
            case "next":
            case "back":
                if (args.Length != 2 || !long.TryParse(args[1], out mediaId) ||
                    !await browseService.OwnsCallbackAsync(
                        userId,
                        query.Message.Id,
                        mediaId,
                        ctx.CancellationToken))
                {
                    await AnswerAsync(
                        query,
                        "Эта сессия просмотра устарела или принадлежит другому пользователю.",
                        true,
                        ctx);
                    return;
                }
                break;
            default:
                await AnswerAsync(query, "Неизвестное или устаревшее действие.", true, ctx);
                return;
        }

        // Telegram callback query IDs expire quickly. Acknowledge the click after
        // authorization and payload validation, before database/media work.
        await AnswerAsync(query, null, false, ctx);

        switch (action)
        {
            case "menu":
                await ctx.Sender.SendText(query.Message.Chat.Id, new SendText
                {
                    Text = "Выбор фото и видео",
                    ReplyMarkup = BotUiService.MainMenu(user.Role == UserRole.Admin)
                }, ctx.CancellationToken);
                break;
            case "browse":
                await StartBrowseAsync(userId, query.Message.Chat.Id, BrowseFilter.Unreviewed, null, null, ctx);
                break;
            case "liked":
                await StartBrowseAsync(userId, query.Message.Chat.Id, BrowseFilter.Liked, null, null, ctx);
                break;
            case "skipped":
                await StartBrowseAsync(userId, query.Message.Chat.Id, BrowseFilter.Skipped, null, null, ctx);
                break;
            case "all":
                await StartBrowseAsync(userId, query.Message.Chat.Id, BrowseFilter.All, null, null, ctx);
                break;
            case "topics":
                await SendTopicsAsync(userId, query.Message.Chat.Id, ctx);
                break;
            case "sources":
                await SendSourcesAsync(userId, query.Message.Chat.Id, ctx);
                break;
            case "source":
                await StartBrowseAsync(userId, query.Message.Chat.Id, BrowseFilter.Unreviewed, sourceChatId, null, ctx);
                break;
            case "topic":
                await StartBrowseAsync(userId, query.Message.Chat.Id, BrowseFilter.Unreviewed, topicChatId, threadId, ctx);
                break;
            case "export":
                await ExportAsync(userId, query.Message.Chat.Id, ctx);
                break;
            case "admin":
                await ctx.Sender.SendText(query.Message.Chat.Id, new SendText
                {
                    Text = "Команды администратора:\n" +
                           "/access_grant <id> — выдать доступ\n" +
                           "/access_revoke <id> — отозвать доступ\n" +
                           "/access_list — список пользователей\n\n" +
                           "В группе: /source_enable или /source_disable"
                }, ctx.CancellationToken);
                break;
            case "like":
            case "skip":
            case "next":
            case "back":
                await NavigateAsync(query, action, mediaId, ctx);
                break;
        }
    }

    private async Task NavigateAsync(CallbackQuery query, string action, long mediaId, BotContext ctx)
    {
        var userId = query.From.Id;
        var callbackMessage = query.Message!;

        if (action is "like" or "skip")
        {
            await selectionService.SetAsync(
                userId,
                mediaId,
                action == "like" ? SelectionState.Liked : SelectionState.Skipped,
                ctx.CancellationToken);
        }

        var page = await browseService.MoveAsync(
            userId,
            mediaId,
            action != "back",
            ctx.CancellationToken);
        if (page is null)
        {
            await ctx.Sender.EditReplyMarkup(
                callbackMessage.Chat.Id,
                callbackMessage.Id,
                BotUiService.EndKeyboard(mediaId),
                ctx.CancellationToken);
            await ctx.Sender.SendText(callbackMessage.Chat.Id, new SendText
            {
                Text = "Вы дошли до конца этой подборки.",
                ReplyMarkup = BotUiService.MainMenu(false)
            }, ctx.CancellationToken);
            return;
        }

        await ui.EditPageAsync(callbackMessage.Chat.Id, callbackMessage.Id, page, ctx);
    }

    private async Task StartBrowseFromMessageAsync(Message message, BrowseFilter filter, BotContext ctx)
    {
        if (!await RequirePrivateAuthorizedAsync(message, ctx))
            return;
        await StartBrowseAsync(message.From!.Id, message.Chat.Id, filter, null, null, ctx);
    }

    private async Task StartBrowseAsync(
        long userId,
        long chatId,
        BrowseFilter filter,
        long? sourceChatId,
        int? sourceThreadId,
        BotContext ctx)
    {
        var page = await browseService.StartAsync(
            userId,
            filter,
            sourceChatId,
            sourceThreadId,
            ctx.CancellationToken);
        if (page is null)
        {
            await ctx.Sender.SendText(chatId, new SendText
            {
                Text = "В этой подборке пока нет фото или видео.",
                ReplyMarkup = BotUiService.MainMenu(false)
            }, ctx.CancellationToken);
            return;
        }

        await ui.SendPageAsync(userId, chatId, page, ctx);
    }

    private async Task SendTopicsAsync(long userId, long chatId, BotContext ctx)
    {
        if (!await accessService.IsAuthorizedAsync(userId, ctx.CancellationToken))
            return;

        await using var db = await contextFactory.CreateDbContextAsync(ctx.CancellationToken);
        var topics = await db.SourceTopics.AsNoTracking()
            .Where(x => x.SourceChat.IsEnabled)
            .OrderBy(x => x.SourceChat.Title)
            .ThenBy(x => x.ThreadId)
            .Take(50)
            .Select(x => new { x.ChatId, x.ThreadId, ChatTitle = x.SourceChat.Title, x.Title })
            .ToListAsync(ctx.CancellationToken);

        if (topics.Count == 0)
        {
            await ctx.Sender.SendText(chatId, new SendText { Text = "Темы пока не проиндексированы." }, ctx.CancellationToken);
            return;
        }

        var rows = topics.Select(x => Keyboard.Row(Keyboard.Callback(
            $"{x.ChatTitle}: {x.Title}",
            "ps",
            "topic",
            x.ChatId.ToString(),
            x.ThreadId.ToString())));
        await ctx.Sender.SendText(chatId, new SendText
        {
            Text = "Выберите тему (непросмотренные материалы):",
            ReplyMarkup = Keyboard.Inline(rows)
        }, ctx.CancellationToken);
    }

    private async Task SendSourcesAsync(long userId, long chatId, BotContext ctx)
    {
        if (!await accessService.IsAuthorizedAsync(userId, ctx.CancellationToken))
            return;

        await using var db = await contextFactory.CreateDbContextAsync(ctx.CancellationToken);
        var sources = await db.SourceChats.AsNoTracking()
            .Where(x => x.IsEnabled)
            .OrderBy(x => x.Title)
            .Take(50)
            .Select(x => new { x.ChatId, x.Title })
            .ToListAsync(ctx.CancellationToken);

        if (sources.Count == 0)
        {
            await ctx.Sender.SendText(chatId, new SendText { Text = "Нет включённых чатов-источников." }, ctx.CancellationToken);
            return;
        }

        await ctx.Sender.SendText(chatId, new SendText
        {
            Text = "Выберите источник (непросмотренные материалы):",
            ReplyMarkup = Keyboard.Inline(sources.Select(x =>
                Keyboard.Row(Keyboard.Callback(x.Title, "ps", "source", x.ChatId.ToString()))))
        }, ctx.CancellationToken);
    }

    private async Task ExportAsync(long userId, long chatId, BotContext ctx)
    {
        var items = await exportService.GetLikedAsync(userId, ctx.CancellationToken);
        if (items.Count == 0)
        {
            await ctx.Sender.SendText(chatId, new SendText { Text = "У вас нет выбранных фото или видео." }, ctx.CancellationToken);
            return;
        }

        foreach (var item in items)
        {
            if (item.MediaKind is MediaKind.ImageDocument or MediaKind.VideoDocument)
            {
                await ctx.BotClient.SendDocument(
                    chatId,
                    InputFile.FromFileId(item.FileId),
                    caption: item.FileName,
                    cancellationToken: ctx.CancellationToken);
            }
            else if (item.MediaKind == MediaKind.Video)
            {
                await ctx.BotClient.SendVideo(
                    chatId,
                    InputFile.FromFileId(item.FileId),
                    caption: item.FileName,
                    supportsStreaming: true,
                    cancellationToken: ctx.CancellationToken);
            }
            else
            {
                await ctx.Sender.SendPhoto(chatId, new SendPhoto
                {
                    Photo = InputFile.FromFileId(item.FileId)
                }, ctx.CancellationToken);
            }
        }
    }

    private async Task SetSourceAsync(Message message, bool enabled, BotContext ctx)
    {
        if (message.From is null || message.Chat.Type is not (ChatType.Group or ChatType.Supergroup))
        {
            await ReplyAsync(message, "Используйте эту команду внутри группы или супергруппы-источника.", ctx);
            return;
        }

        try
        {
            await sourceChatService.SetEnabledAsync(
                message.From.Id,
                message.Chat.Id,
                message.Chat.Title ?? message.Chat.Id.ToString(),
                enabled,
                ctx.CancellationToken);
            await ReplyAsync(message, enabled ? "Этот источник включён." : "Этот источник отключён.", ctx);
        }
        catch (UnauthorizedAccessException)
        {
            await ReplyAsync(message, "Требуются права администратора.", ctx);
        }
    }

    private async Task RunAdminActionAsync(
        Message message,
        BotContext ctx,
        Func<long, Task<string>> action)
    {
        if (message.From is null || !await accessService.IsAdminAsync(message.From.Id, ctx.CancellationToken))
        {
            await ReplyAsync(message, "Требуются права администратора.", ctx);
            return;
        }

        try
        {
            await ReplyAsync(message, await action(message.From.Id), ctx);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentOutOfRangeException)
        {
            await ReplyAsync(message, exception.Message, ctx);
        }
    }

    private async Task<bool> RequirePrivateAuthorizedAsync(Message message, BotContext ctx)
    {
        if (!TryGetPrivateHuman(message, out var userId) ||
            !await accessService.IsAuthorizedAsync(userId, ctx.CancellationToken))
        {
            await ReplyAsync(message, "Доступ запрещён. Используйте бота в личном чате с разрешённого аккаунта.", ctx);
            return false;
        }
        return true;
    }

    private static bool TryGetPrivateHuman(Message message, out long userId)
    {
        userId = message.From?.Id ?? 0;
        return message.Chat.Type == ChatType.Private && message.From is { IsBot: false };
    }

    private static bool TryParseUserId(string? text, string command, out long userId)
    {
        userId = 0;
        var parts = text?.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts is { Length: 2 } &&
               parts[0].Split('@')[0].Equals(command, StringComparison.OrdinalIgnoreCase) &&
               long.TryParse(parts[1], out userId) && userId > 0;
    }

    private static Task ReplyAsync(Message message, string text, BotContext ctx)
        => ctx.Sender.ReplyText(message, new SendText { Text = text }, ctx.CancellationToken);

    private static async Task AnswerAsync(
        CallbackQuery query,
        string? text,
        bool showAlert,
        BotContext ctx)
    {
        try
        {
            await ctx.Sender.AnswerCallback(
                query.Id,
                new AnswerCallback { Text = text, ShowAlert = showAlert },
                ctx.CancellationToken);
        }
        catch (ApiRequestException exception) when (
            exception.ErrorCode == 400 &&
            (exception.Message.Contains("query is too old", StringComparison.OrdinalIgnoreCase) ||
             exception.Message.Contains("query ID is invalid", StringComparison.OrdinalIgnoreCase)))
        {
            // Действие всё ещё может быть полезно, даже если Telegram уже не может
            // подтвердить нажатие (например, после паузы в отладчике).
        }
    }
}
