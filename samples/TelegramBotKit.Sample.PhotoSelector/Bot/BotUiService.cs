using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramBotKit.Keyboards;
using TelegramBotKit.Sample.PhotoSelector.Browsing;
using TelegramBotKit.Sample.PhotoSelector.Data.Entities;
using TelegramBotKit.Sample.PhotoSelector.Media;

namespace TelegramBotKit.Sample.PhotoSelector.Bot;

public sealed class BotUiService(
    BrowseService browseService,
    MediaPreviewService previewService)
{
    public static InlineKeyboardMarkup MainMenu(bool isAdmin)
    {
        var rows = new List<IReadOnlyList<InlineKeyboardButton>>
        {
            Keyboard.Row(
                Keyboard.Callback("📷 Смотреть", "ps", "browse"),
                Keyboard.Callback("❤️ Выбранные", "ps", "liked")),
            Keyboard.Row(
                Keyboard.Callback("📂 Темы", "ps", "topics"),
                Keyboard.Callback("🗂 Источники", "ps", "sources")),
            Keyboard.Row(Keyboard.Callback("📦 Скачать выбранное", "ps", "export")),
            Keyboard.Row(
                Keyboard.Callback("👎 Пропущенные", "ps", "skipped"),
                Keyboard.Callback("🖼 Всё", "ps", "all"))
        };

        if (isAdmin)
            rows.Add(Keyboard.Row(Keyboard.Callback("⚙️ Команды администратора", "ps", "admin")));

        return Keyboard.Inline(rows);
    }

    public async Task SendPageAsync(long userId, long chatId, BrowsePage page, BotContext ctx)
    {
        var sent = await SendMediaAsync(chatId, page, ctx);
        await browseService.SetBrowseMessageIdAsync(userId, sent.Id, ctx.CancellationToken);
    }

    public async Task EditPageAsync(long chatId, int messageId, BrowsePage page, BotContext ctx)
    {
        if (NeedsGeneratedPreview(page.Media))
        {
            try
            {
                await using var stream = await previewService.DownloadOriginalAsync(
                    ctx.BotClient,
                    page.Media,
                    ctx.CancellationToken);
                if (stream is not null)
                {
                    var uploaded = await EditUploadedPreviewAsync(chatId, messageId, page, stream, ctx);
                    await previewService.SaveReusablePreviewAsync(
                        page.Media.Id,
                        uploaded,
                        page.Media.MediaKind,
                        ctx.CancellationToken);
                    return;
                }
            }
            catch (ApiRequestException)
            {
                // Telegram не смог скачать или преобразовать этот файл — ниже
                // показываем исходный документ без потери качества.
            }
            catch (HttpRequestException)
            {
                // При временной ошибке скачивания исходник остаётся доступен как документ.
            }
        }

        await ctx.BotClient.EditMessageMedia(
            chatId: chatId,
            messageId: messageId,
            media: CreateStoredMedia(page),
            replyMarkup: PageKeyboard(page.Media.Id),
            cancellationToken: ctx.CancellationToken);
    }

    public static InlineKeyboardMarkup EndKeyboard(long mediaId)
        => Keyboard.Inline(
        [
            [Keyboard.Callback("⬅ Назад", "ps", "back", mediaId.ToString())],
            [Keyboard.Callback("🏠 Меню", "ps", "menu")]
        ]);

    private async Task<Message> SendMediaAsync(long chatId, BrowsePage page, BotContext ctx)
    {
        if (NeedsGeneratedPreview(page.Media))
        {
            try
            {
                await using var stream = await previewService.DownloadOriginalAsync(
                    ctx.BotClient,
                    page.Media,
                    ctx.CancellationToken);
                if (stream is not null)
                {
                    var sent = await SendUploadedPreviewAsync(chatId, page, stream, ctx);
                    await previewService.SaveReusablePreviewAsync(
                        page.Media.Id,
                        sent,
                        page.Media.MediaKind,
                        ctx.CancellationToken);
                    return sent;
                }
            }
            catch (ApiRequestException)
            {
                // Используем исходный документ как запасной вариант.
            }
            catch (HttpRequestException)
            {
                // Используем исходный документ как запасной вариант.
            }
        }

        return page.Media.MediaKind switch
        {
            MediaKind.Photo => await SendPhotoAsync(
                chatId,
                InputFile.FromFileId(page.Media.PreviewFileId ?? page.Media.OriginalFileId),
                page,
                ctx),
            MediaKind.Video => await SendVideoAsync(
                chatId,
                InputFile.FromFileId(page.Media.PreviewFileId ?? page.Media.OriginalFileId),
                page,
                ctx),
            MediaKind.ImageDocument when page.Media.PreviewIsReusable => await SendPhotoAsync(
                chatId,
                InputFile.FromFileId(page.Media.PreviewFileId!),
                page,
                ctx),
            MediaKind.VideoDocument when page.Media.PreviewIsReusable => await SendVideoAsync(
                chatId,
                InputFile.FromFileId(page.Media.PreviewFileId!),
                page,
                ctx),
            _ => await SendDocumentAsync(chatId, page, ctx)
        };
    }

    private Task<Message> SendUploadedPreviewAsync(
        long chatId,
        BrowsePage page,
        Stream stream,
        BotContext ctx)
        => page.Media.MediaKind == MediaKind.VideoDocument
            ? SendVideoAsync(chatId, InputFile.FromStream(stream, PreviewFileName(page.Media, ".mp4")), page, ctx)
            : SendPhotoAsync(chatId, InputFile.FromStream(stream, PreviewFileName(page.Media, ".jpg")), page, ctx);

    private async Task<Message> EditUploadedPreviewAsync(
        long chatId,
        int messageId,
        BrowsePage page,
        Stream stream,
        BotContext ctx)
    {
        InputMedia media = page.Media.MediaKind == MediaKind.VideoDocument
            ? new InputMediaVideo(InputFile.FromStream(stream, PreviewFileName(page.Media, ".mp4")))
            {
                Caption = Caption(page),
                SupportsStreaming = true
            }
            : new InputMediaPhoto(InputFile.FromStream(stream, PreviewFileName(page.Media, ".jpg")))
            {
                Caption = Caption(page)
            };

        return await ctx.BotClient.EditMessageMedia(
            chatId: chatId,
            messageId: messageId,
            media: media,
            replyMarkup: PageKeyboard(page.Media.Id),
            cancellationToken: ctx.CancellationToken);
    }

    private static InputMedia CreateStoredMedia(BrowsePage page)
    {
        var fileId = page.Media.PreviewIsReusable
            ? page.Media.PreviewFileId ?? page.Media.OriginalFileId
            : page.Media.OriginalFileId;

        return page.Media.MediaKind switch
        {
            MediaKind.Photo =>
                new InputMediaPhoto(InputFile.FromFileId(fileId)) { Caption = Caption(page) },
            MediaKind.ImageDocument when page.Media.PreviewIsReusable =>
                new InputMediaPhoto(InputFile.FromFileId(fileId)) { Caption = Caption(page) },
            MediaKind.Video =>
                new InputMediaVideo(InputFile.FromFileId(fileId))
                {
                    Caption = Caption(page),
                    SupportsStreaming = true
                },
            MediaKind.VideoDocument when page.Media.PreviewIsReusable =>
                new InputMediaVideo(InputFile.FromFileId(fileId))
                {
                    Caption = Caption(page),
                    SupportsStreaming = true
                },
            _ => new InputMediaDocument(InputFile.FromFileId(page.Media.OriginalFileId))
            {
                Caption = Caption(page)
            }
        };
    }

    private static Task<Message> SendPhotoAsync(long chatId, InputFile photo, BrowsePage page, BotContext ctx)
        => ctx.BotClient.SendPhoto(
            chatId: chatId,
            photo: photo,
            caption: Caption(page),
            replyMarkup: PageKeyboard(page.Media.Id),
            cancellationToken: ctx.CancellationToken);

    private static Task<Message> SendVideoAsync(long chatId, InputFile video, BrowsePage page, BotContext ctx)
        => ctx.BotClient.SendVideo(
            chatId: chatId,
            video: video,
            caption: Caption(page),
            replyMarkup: PageKeyboard(page.Media.Id),
            supportsStreaming: true,
            cancellationToken: ctx.CancellationToken);

    private static Task<Message> SendDocumentAsync(long chatId, BrowsePage page, BotContext ctx)
        => ctx.BotClient.SendDocument(
            chatId: chatId,
            document: InputFile.FromFileId(page.Media.OriginalFileId),
            caption: Caption(page),
            replyMarkup: PageKeyboard(page.Media.Id),
            cancellationToken: ctx.CancellationToken);

    private static bool NeedsGeneratedPreview(MediaItem media)
        => media.MediaKind is MediaKind.ImageDocument or MediaKind.VideoDocument &&
           !media.PreviewIsReusable;

    private static string PreviewFileName(MediaItem media, string fallbackExtension)
        => string.IsNullOrWhiteSpace(media.FileName)
            ? $"preview{fallbackExtension}"
            : media.FileName;

    private static InlineKeyboardMarkup PageKeyboard(long mediaId)
        => Keyboard.Inline(
        [
            [
                Keyboard.Callback("👎 Пропустить", "ps", "skip", mediaId.ToString()),
                Keyboard.Callback("❤️ Выбрать", "ps", "like", mediaId.ToString())
            ],
            [
                Keyboard.Callback("⬅ Назад", "ps", "back", mediaId.ToString()),
                Keyboard.Callback("Дальше ➡", "ps", "next", mediaId.ToString())
            ],
            [Keyboard.Callback("🏠 Меню", "ps", "menu")]
        ]);

    private static string Caption(BrowsePage page)
        => $"{page.Position} из {page.Total}\n\n" +
           $"Источник: {page.SourceTitle}\n" +
           $"Тема: {page.TopicTitle}\n\n" +
           $"❤️ Выбрано: {page.SelectedCount}";
}
