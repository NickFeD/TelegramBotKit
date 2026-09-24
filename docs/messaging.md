# Messaging

[Docs index](README.md) · [Keyboards](keyboards.md)

`IMessageSender` is the application-facing façade for sending and editing Telegram
messages and answering callbacks. Access the per-update instance through
`BotContext.Sender`.

```csharp
await context.Sender.SendText(
    message.Chat.Id,
    new SendText { Text = "Hello." },
    context.CancellationToken);
```

Request models include `SendText`, `SendPhoto`, `EditText`, `EditPhoto`, and
`AnswerCallback`. Extension methods accept `Message` or `CallbackQuery` when those
objects already contain the required identifiers.

## Callback queries and inline mode

A callback created from a chat message has `CallbackQuery.Message`; an inline callback
does not. Methods such as `SendText(callback, ...)` that require chat and message IDs
throw when that message is unavailable.

Use the corresponding `Try*` method when either callback form is valid:

```csharp
if (context.Sender.TryEditText(
    callback,
    new EditText { Text = "Updated" },
    out var edit,
    context.CancellationToken) && edit is not null)
{
    await edit;
}
```

`TrySendText`, `TryReplyText`, `TryEditText`, `TryEditReplyMarkup`, and
`TryEditPhoto` return false and set their task to null when
`CallbackQuery.Message` is unavailable.

## Optional queued sender

The default sender performs operations directly. Enable the queued implementation
when the application needs centralized rate limiting and retry behavior:

```csharp
bot.UseQueuedMessageSender(options =>
{
    options.GlobalMaxPerSecond = 25;
    options.PerChatMinDelay = TimeSpan.FromSeconds(1);
    options.MaxRetryAttempts = 5;
});
```

Queued sending is optional and is not required by the minimal bot setup.
