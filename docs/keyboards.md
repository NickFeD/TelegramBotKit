# Keyboards

TelegramBotKit contains small helpers for creating Telegram reply markups:

- `TelegramBotKit.Keyboards.Keyboard` for **inline keyboards** (callback / URL buttons)
- `TelegramBotKit.Keyboards.Keyboard` for **reply keyboards** (text buttons, request contact/location/poll)

These helpers are optional: you can always construct Telegram `ReplyMarkups` manually.

---

## Inline keyboards

### Callback buttons

`Keyboard.Callback(text, key, args...)` packs callback data in the same format that the router expects:

```
{key} {arg1} {arg2} ...
```

Rules:

- `key` and `args` must not contain spaces
- callback data is validated against Telegram's **64-byte** limit (UTF-8)

Example:

```csharp
using TelegramBotKit.Keyboards;
using TelegramBotKit.Messaging;

var kb = Keyboard.Inline(new[]
{
    Keyboard.Row(
        Keyboard.Callback("Like", "like", "42"),
        Keyboard.Callback("More", "more", "42")
    )
});

// send it:
await ctx.Sender.SendText(chatId, new SendText
{
    Text = "Choose:",
    ReplyMarkup = kb
}, ctx.CancellationToken);
```

If you already have a raw callback string, use `Keyboard.CallbackRaw(text, data)`.

### URL and other inline buttons

```csharp
using TelegramBotKit.Keyboards;
using TelegramBotKit.Messaging;

var kb = Keyboard.Inline(new[]
{
    Keyboard.Row(
        Keyboard.Url("Open site", "https://example.com"),
        Keyboard.SwitchInline("Search here", "cats")
    )
});
```

---

## Reply keyboards

Reply keyboards show buttons under the input field.

```csharp
using TelegramBotKit.Keyboards;
using TelegramBotKit.Messaging;

var kb = Keyboard.Reply(
    new[]
    {
        Keyboard.ReplyRow(
            Keyboard.Text("Hi"),
            Keyboard.RequestLocation("Send location")
        ),
        Keyboard.ReplyRow(
            Keyboard.RequestContact("Share contact")
        )
    },
    resizeKeyboard: true,
    oneTimeKeyboard: true);

await ctx.Sender.SendText(chatId, new SendText
{
    Text = "Pick one:",
    ReplyMarkup = kb
}, ctx.CancellationToken);
```

To remove a reply keyboard:

```csharp
await ctx.Sender.SendText(chatId, new SendText
{
    Text = "Keyboard removed.",
    ReplyMarkup = Keyboard.RemoveReplyKeyboard()
}, ctx.CancellationToken);
```
