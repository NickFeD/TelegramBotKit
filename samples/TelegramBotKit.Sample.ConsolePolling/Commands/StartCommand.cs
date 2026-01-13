using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramBotKit.Commands;
using TelegramBotKit.Keyboards;

namespace TelegramBotKit.Sample.ConsolePolling.Commands;

[Command]
public sealed class StartCommand : IMessageCommand
{
    public string Command => "/start";

    public async Task HandleAsync(Message message, BotContext ctx, CancellationToken ct)
    {
        var kb = Keyboard.Inline(
        [
            [ Keyboard.Callback("✅ Callback", "test_callback"),
              Keyboard.Callback("⏳ Wait", "test_wait") ],
            [ Keyboard.Callback("🧾 Trace", "test_trace"),
              Keyboard.Callback("❤️ Like(123)", "like", "123") ]
        ]);

        await ctx.BotClient.SendMessage(
            chatId: message.Chat.Id,
            text: "Тест-меню:\n" +
                  "✅ Callback — проверка callback\n" +
                  "⏳ Wait — проверка WaitForUserResponse\n" +
                  "🧾 Trace — проверка middleware Items\n" +
                  "❤️ Like — callback с аргументом\n\n" +
                  "Также есть текст-триггер: напиши 'echo'",
            replyMarkup: kb,
            cancellationToken: ct);
    }
}
