# Conversations (WaitForUserResponse)

TelegramBotKit includes a small helper named `WaitForUserResponse`.
It lets you implement simple “ask a question → wait for the next message” flows.

## How it works

- Your code calls `WaitForUserResponse.WaitAsync(chatId, userId, timeout)`.
- Internally, it registers a waiter for that `(chatId, userId)` pair.
- When TelegramBotKit receives the next `Message` from the same chat/user, it tries to **publish** it to the waiter.
- If the waiter receives a message, `WaitAsync` returns that `Message`.
- If the wait times out or is cancelled, `WaitAsync` returns `null`.

Important: TelegramBotKit attempts to publish to an active waiter **before** running command routing.
So the “next message” will usually be consumed by the waiter, not by your message commands.

## Constraints / gotchas

### Only one active wait per chat+user

Only **one** wait is allowed per `(chatId, userId)`.
If you call `WaitAsync` again while the previous wait is still active, you will get:

```
InvalidOperationException: Already waiting for message from chat:... user:...
```

This is intentional: it prevents ambiguous “who should receive the next message?” situations.

### Avoid starting multiple flows

If your bot starts a wait from multiple places (for example, two different callback buttons), decide on a policy:

- **Single active flow:** ignore / reject the new request if a flow is already active.
- **Restart flow:** tell the user to finish/cancel the current step first.
- **State machine:** keep a per-user state and route messages based on that state (more work, but scalable).

### Timeout is a normal outcome

Treat `null` as “no response”:

- user didn’t answer
- user answered too late
- request was cancelled (shutdown, cancellation token)

## Example: ask from a callback, then wait for the next message

```csharp
using Telegram.Bot.Types;
using TelegramBotKit.Commands;
using TelegramBotKit.Conversations;
using TelegramBotKit.Messaging;

[CallbackCommand("name")]
public sealed class AskNameCallback : ICallbackCommand
{
    private readonly WaitForUserResponse _wait;

    public AskNameCallback(WaitForUserResponse wait)
        => _wait = wait;

    public async Task HandleAsync(CallbackQuery query, string[] args, BotContext ctx)
    {
        var chatId = query.Message?.Chat.Id ?? 0;
        var userId = query.From.Id;
        if (chatId == 0)
            return;

        await ctx.Sender.SendText(chatId, new SendText { Text = "What is your name?" }, ctx.CancellationToken);

        Message? reply;
        try
        {
            reply = await _wait.WaitAsync(chatId, userId, TimeSpan.FromSeconds(30), ctx.CancellationToken);
        }
        catch (InvalidOperationException)
        {
            await ctx.Sender.SendText(chatId, new SendText
            {
                Text = "We are already waiting for your previous answer. Please reply to that first."
            }, ctx.CancellationToken);
            return;
        }

        if (reply is null)
        {
            await ctx.Sender.SendText(chatId, new SendText { Text = "Timeout. Try again." }, ctx.CancellationToken);
            return;
        }

        var name = reply.Text?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            await ctx.Sender.SendText(chatId, new SendText { Text = "Please send text." }, ctx.CancellationToken);
            return;
        }

        await ctx.Sender.SendText(chatId, new SendText { Text = $"Nice to meet you, {name}!" }, ctx.CancellationToken);
    }
}
```

## Interaction with the hosting scheduler

When you use `TelegramBotKit.Hosting` polling, updates are processed using “actors” (see `./hosting.md`).
Callback queries are keyed by **user**, which helps keep request/response flows predictable for a single user.

That said, the “one active wait” rule still applies: even with perfect serialization, starting a second wait before finishing the first will throw.
