using Telegram.Bot.Types;
using TelegramBotKit.Commands;
using TelegramBotKit.Keyboards;
using TelegramBotKit.Messaging;

namespace TelegramBotKit.Sample.ConsolePolling.Commands;

[CallbackCommand("photo_more")]
public sealed class PhotoMoreCallbackCommand : ICallbackCommand
{
    public async Task HandleAsync(CallbackQuery query, string[] args, BotContext ctx)
    {
        if (query.Message is null)
        {
            await ctx.Sender.AnswerCallback(query.Id, new AnswerCallback { Text = "Нет message" }, ctx.CancellationToken);
            return;
        }

        await ctx.Sender.AnswerCallback(query.Id, new AnswerCallback { Text = "Ок, отправляю ещё 📷" }, ctx.CancellationToken);

        var kb = Keyboard.Inline(
        [
            [ Keyboard.Callback("🔄 Ещё фото", "photo_more"),
              Keyboard.Callback("❤️ Like(99)", "like", "99") ]
        ]);

        await ctx.Sender.SendPhoto(
            chatId: query.Message.Chat.Id,
            msg: new SendPhoto
            {
                Photo = InputFile.FromUri(new Uri("https://picsum.photos/id/1025/800/600")),
                Caption = "Ещё одно фото 🐶",
                ReplyMarkup = kb
            },
            ct: ctx.CancellationToken);
    }
}
