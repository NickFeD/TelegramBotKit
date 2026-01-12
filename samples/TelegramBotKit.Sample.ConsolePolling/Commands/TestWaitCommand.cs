using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramBotKit;
using TelegramBotKit.Commands;
using TelegramBotKit.Conversations;

[Command]
public sealed class TestWaitCommand : ICallbackCommand
{
    public string Key => "test_wait";

    public async Task HandleAsync(CallbackQuery query, string[] args, BotContext ctx, CancellationToken ct)
    {
        await ctx.BotClient.AnswerCallbackQuery(query.Id, cancellationToken: ct);

        var msg = query.Message;
        var from = query.From;

        if (msg is null || from is null)
            return;

        var chatId = msg.Chat.Id;
        var userId = from.Id;

        var wait = ctx.GetRequiredService<WaitForUserResponse>();

        await ctx.BotClient.SendMessage(
            chatId: chatId,
            text: "Ок! Напиши своё имя (у тебя 30 секунд):",
            cancellationToken: ct);

        var nameMsg = await wait.WaitMessageAsync(chatId, userId, TimeSpan.FromSeconds(30), ct);

        if (nameMsg?.Text is null)
        {
            await ctx.BotClient.SendMessage(
                chatId: chatId,
                text: "⏳ Таймаут. Ничего не пришло.",
                cancellationToken: ct);
            return;
        }

        var name = nameMsg.Text.Trim();

        await ctx.BotClient.SendMessage(
            chatId: chatId,
            text: $"Принято ✅ Твоё имя: {name}\nТеперь напиши возраст (15 секунд):",
            cancellationToken: ct);

        var ageMsg = await wait.WaitMessageAsync(chatId, userId, TimeSpan.FromSeconds(15), ct);

        if (ageMsg?.Text is null)
        {
            await ctx.BotClient.SendMessage(chatId, "⏳ Таймаут на возраст.", cancellationToken: ct);
            return;
        }

        await ctx.BotClient.SendMessage(
            chatId: chatId,
            text: $"Готово ✅ Имя: {name}, возраст: {ageMsg.Text.Trim()}",
            cancellationToken: ct);
    }
}
