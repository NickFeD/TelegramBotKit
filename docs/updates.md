# Updates and Payload Handlers

TelegramBotKit routes incoming `Update` objects in two steps:

1) **Select a route by `UpdateType`** (e.g. `Message`, `CallbackQuery`, `InlineQuery`)
2) **Extract a payload** from the `Update` and dispatch it to all registered handlers of that payload type:
   `IUpdatePayloadHandler<TPayload>`

This design is simple and fast, but it has an important implication:

> Handlers are selected by **payload type** (`TPayload`), not by `UpdateType`.

That matters for Telegram update types that share the same payload type (for example, both `Message` and `EditedMessage` payloads are of type `Message`).

---

## Adding handlers for other update types

To handle a new update type, you usually need to do two things:

1) Register a handler in DI: `IUpdatePayloadHandler<TPayload>`
2) Add a mapping from `UpdateType` to a payload extractor: `Update -> TPayload?`

### Example: InlineQuery (unique payload type)

`UpdateType.InlineQuery` maps to `Update.InlineQuery` which is `InlineQuery`.

```csharp
using Telegram.Bot.Types;
using TelegramBotKit.DependencyInjection;
using TelegramBotKit.Dispatching;

public sealed class InlineQueryHandler : IUpdatePayloadHandler<InlineQuery>
{
    public Task HandleAsync(InlineQuery payload, BotContext ctx)
    {
        // handle inline query
        return Task.CompletedTask;
    }
}

// registration
var kit = builder.Services.AddTelegramBotKit(opt => opt.Token = "...");

kit.Services.AddUpdateHandler<InlineQuery, InlineQueryHandler>();
kit.Map<InlineQuery>(UpdateType.InlineQuery, static u => u.InlineQuery);
```

This is the ideal case: **one UpdateType → one payload type**.

---

## When multiple UpdateTypes share the same payload type

Telegram has update types that share the same payload CLR type. The most common example:

* `UpdateType.Message` → `Update.Message` → `Message`
* `UpdateType.EditedMessage` → `Update.EditedMessage` → `Message`

If you register `IUpdatePayloadHandler<Message>`, it will run for **every route that dispatches `Message`**.

Important:

- Adding a mapping for `UpdateType.EditedMessage` **does not overwrite** the existing `UpdateType.Message` mapping.
- Adding another `IUpdatePayloadHandler<Message>` **does not replace** other `Message` handlers — it **adds** a new one.

That means this registration:

```csharp
kit.Services.AddUpdateHandler<Message, EditedMessageHandler>();
kit.Map<Message>(UpdateType.EditedMessage, static u => u.EditedMessage);
```

does **not** isolate the handler to edited messages only.
It registers another `IUpdatePayloadHandler<Message>`, so it may also run for normal `Message` updates (and other routes that dispatch `Message`).

You have two recommended ways to handle this.

---

## Option A: Filter by UpdateType inside the handler (simple)

Use `TPayload = Message`, but guard by the current update type:

```csharp
using Telegram.Bot.Types;
using TelegramBotKit.DependencyInjection;
using TelegramBotKit.Dispatching;

public sealed class EditedMessageHandler : IUpdatePayloadHandler<Message>
{
    public Task HandleAsync(Message payload, BotContext ctx)
    {
        if (ctx.Update.Type != UpdateType.EditedMessage)
            return Task.CompletedTask;

        // handle edited message here
        return Task.CompletedTask;
    }
}

var kit = builder.Services.AddTelegramBotKit(opt => opt.Token = "...");

kit.Services.AddUpdateHandler<Message, EditedMessageHandler>();
kit.Map<Message>(UpdateType.EditedMessage, static u => u.EditedMessage);
```

Pros:

* Minimal code, no extra types

Cons:

* Handler is still invoked for other `Message` routes, but exits quickly

---

## Option B: Use a wrapper payload type (clean isolation)

Create a dedicated payload type so your handler is selected by **a unique CLR type**:

```csharp
using Telegram.Bot.Types;
using TelegramBotKit.DependencyInjection;

public sealed class EditedMessagePayload
{
    public EditedMessagePayload(Message message) => Message = message;
    public Message Message { get; }
}
```

Handler:

```csharp
using TelegramBotKit.Dispatching;

public sealed class EditedMessageHandler : IUpdatePayloadHandler<EditedMessagePayload>
{
    public Task HandleAsync(EditedMessagePayload payload, BotContext ctx)
    {
        // payload.Message is the edited message
        return Task.CompletedTask;
    }
}
```

Registration + mapping:

```csharp
var kit = builder.Services.AddTelegramBotKit(opt => opt.Token = "...");

kit.Services.AddUpdateHandler<EditedMessagePayload, EditedMessageHandler>();

kit.Map<EditedMessagePayload>(
    UpdateType.EditedMessage,
    static u => u.EditedMessage is null ? null : new EditedMessagePayload(u.EditedMessage));
```

Pros:

* Clean separation: only your wrapper payload route triggers this handler
* No accidental mixing with normal `Message` handlers

Cons:

* One small allocation per edited message (the wrapper object)

---

## Multiple handlers for the same payload

You can register multiple handlers for the same `TPayload`:

```csharp
kit.Services.AddUpdateHandler<Message, FirstMessageHandler>();
kit.Services.AddUpdateHandler<Message, SecondMessageHandler>();
```

TelegramBotKit will invoke them sequentially in DI registration order.

---

## AllowedUpdates and polling

If you are using polling (`AddTelegramBotKitPolling`) and set `Polling.AllowedUpdates` to a **non-empty** list, Telegram will only deliver those update types.

So if you add a new mapping/handler for (say) `UpdateType.InlineQuery`, make sure `InlineQuery` is included in `AllowedUpdates`.

If you keep `AllowedUpdates: []`, Telegram delivers **all** update types (default).

---

## One route per UpdateType

TelegramBotKit stores a single route per `UpdateType`. If you call `kit.Map<T>(sameType, ...)` multiple times, the **last mapping wins**.

If you need to fan-out from one `UpdateType` to multiple handlers, keep a single mapping and register multiple `IUpdatePayloadHandler<T>` implementations for that payload type.

---

## Troubleshooting

### “My handler is never called”

Make sure you did both steps:

* registered `IUpdatePayloadHandler<TPayload>` in DI
* added `kit.Map<TPayload>(UpdateType.X, extractor)`

Without a mapping, TelegramBotKit cannot extract the payload from `Update`, so it will not invoke your handler.

### “My handler runs for updates I did not expect”

This typically happens when multiple `UpdateType` routes dispatch the same payload CLR type (like `Message`).

Use Option A (filter by `UpdateType`) or Option B (wrapper payload type) to get the behavior you want.