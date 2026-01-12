using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramBotKit;
using TelegramBotKit.Commands;

[Command]
public sealed class StartCommand : IMessageCommand
{
    public string Command => "/start";

    public async Task HandleAsync(Message message, BotContext ctx, CancellationToken ct)
    {
        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("✅ Callback test", "test_callback"),
                InlineKeyboardButton.WithCallbackData("🧪 WaitForUserResponse", "test_wait")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("ℹ️ Middleware trace", "test_trace")
            }
        });

        await ctx.BotClient.SendMessage(
            chatId: message.Chat.Id,
            text:
                "TelegramBotKit Sample ✅\n\n" +
                "Нажми кнопку ниже, чтобы проверить:\n" +
                "• CallbackQuery\n" +
                "• WaitForUserResponse\n" +
                "• Middleware (traceId)\n",
            replyMarkup: keyboard,
            cancellationToken: ct);
    }
}
