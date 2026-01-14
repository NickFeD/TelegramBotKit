using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramBotKit.Commands;
using TelegramBotKit.Keyboards;
using TelegramBotKit.Messaging;

namespace TelegramBotKit.Sample.ConsolePolling.Commands;

[MessageCommand("/photo")]
public sealed class PhotoCommand : IMessageCommand
{
    public async Task HandleAsync(Message message, BotContext ctx)
    {
        var kb = Keyboard.Inline(
        [
            [ Keyboard.Callback("🔄 Ещё фото", "photo_more"),
              Keyboard.Callback("❤️ Like(42)", "like", "42") ]
        ]);

        await ctx.Sender.SendPhoto(
            chatId: message.Chat.Id,
            msg: new SendPhoto
            {
                Photo = InputFile.FromUri(new Uri("https://picsum.photos/id/237/800/600")),
                Caption = "Фото + inline-кнопки 📷",
                ReplyMarkup = kb
            },
            ct: ctx.CancellationToken);
    }
}
