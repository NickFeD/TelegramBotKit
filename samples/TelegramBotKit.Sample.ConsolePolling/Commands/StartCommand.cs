using Telegram.Bot.Types;
using TelegramBotKit.Commands;
using TelegramBotKit.Keyboards;
using TelegramBotKit.Messaging;

namespace TelegramBotKit.Sample.ConsolePolling.Commands;

[MessageCommand("/start")]
public sealed class StartCommand : IMessageCommand
{
    public Task HandleAsync(Message message, BotContext ctx)
    {
        var kb = Keyboard.Inline(
        [
            [ Keyboard.Callback<TestCallbackCommand>("✅ Callback"),
              Keyboard.Callback("⏳ Wait", "test_wait") ],
            [ Keyboard.Callback("🧾 Trace", "test_trace"),
              Keyboard.Callback<LikeCallbackCommand>("❤️ Like(123)", "123") ]
        ]);

        return ctx.Sender.SendText(
            chatId: message.Chat.Id,
            msg: new SendText
            {
                Text =
                    "Тест-меню:\n" +
                    "✅ Callback — проверка callback\n" +
                    "⏳ Wait — проверка WaitForUserResponse\n" +
                    "🧾 Trace — проверка middleware Items\n" +
                    "❤️ Like — callback с аргументом\n\n" +
                    "Команда: /photo — фото + кнопки\n" +
                    "Также есть текст-триггер: напиши 'echo'",
                ReplyMarkup = kb
            },
            ct: ctx.CancellationToken);
    }
}
