# Commands and routing

TelegramBotKit supports three command styles:

- **Message commands**: slash commands such as `/start`.
- **Text commands**: exact text triggers.
- **Callback commands**: inline keyboard callbacks (`callback_data`).

## Attribute-based commands

Create a command class and add an attribute:

```csharp
using Telegram.Bot.Types;
using TelegramBotKit.Commands;
using TelegramBotKit.Messaging;

[MessageCommand("/start")]
public sealed class StartCommand : IMessageCommand
{
    public Task HandleAsync(Message message, BotContext ctx)
    {
        return ctx.Sender.SendText(message.Chat.Id, new SendText
        {
            Text = "Hello"
        }, ctx.CancellationToken);
    }
}
```

Register all attributed commands:

```csharp
builder.Services.AddCommands();
```

Notes:
- If `TelegramBotKit.Generators` is installed, `AddCommands()` is compile-time.
- Otherwise it falls back to reflection-based discovery.

If you want to avoid reflection (for NativeAOT / trimming), you can fail fast when no generated registrations are present:

```csharp
builder.Services.AddCommands(fallbackToReflection: false);
```

## Routing sugar (TelegramBotKit.Routing)

If you want Minimal-API-like registration without creating a command class, install `TelegramBotKit.Routing` and use `Use*` methods:

```csharp
using TelegramBotKit.Routing;
using TelegramBotKit.Messaging;

bot.UseMessageCommand("/start", async (msg, ctx) =>
{
    await ctx.Sender.SendText(msg.Chat.Id, new SendText { Text = "Hello" }, ctx.CancellationToken);
});

bot.UseCallbackCommand("like", async (q, args, ctx) =>
{
    // callback_data: "like 42"
    var postId = args.Length > 0 ? args[0] : "";
    await ctx.Sender.AnswerCallback(q.Id, new AnswerCallback { Text = $"Liked {postId}" }, ctx.CancellationToken);
});
```

You can also resolve scoped dependencies by using generic overloads:

```csharp
bot.UseMessageCommand<MyService>("/start", (msg, ctx, svc) => svc.HandleStart(msg, ctx));
```

## Callback data format

`UseCallbackCommand` assumes `callback_data` format:

```
{key} {arg1} {arg2} ...
```

Telegram limits `callback_data` to **64 bytes**. Keep keys/args short.

Tips:

- Callback command keys are stored **case-insensitively**.
- If you want a safe way to build callback buttons, use `TelegramBotKit.Keyboards.Keyboard.Callback(...)`.

## Text commands

Text commands match exact message text (after trimming). You can register multiple triggers:

```csharp
bot.UseTextCommand(new[] { "hi", "hello" }, (msg, ctx) => /* ... */ Task.CompletedTask, ignoreCase: true);
```

Notes:

- Text triggers are stored in two buckets:
  - case-sensitive triggers (`ignoreCase: false`)
  - ignore-case triggers (`ignoreCase: true`)
- You cannot register the same trigger in both modes.

## Message (slash) commands

TelegramBotKit treats message commands case-insensitively and strips the optional bot username.

Examples:

- `/start` matches `/start`, `/Start`, `/START`
- `/start@MyBot` is normalized to `/start`

---

## Command lifetime and dependency injection

### Attribute-based commands

When a command matches, TelegramBotKit resolves your command instance from the **per-update DI scope** (`ctx.Services`).
That means **constructor injection works** (ASP.NET-style):

```csharp
public sealed class StartCommand : IMessageCommand
{
    private readonly MyDb _db;
    public StartCommand(MyDb db) => _db = db;

    public Task HandleAsync(Message message, BotContext ctx) => /* ... */ Task.CompletedTask;
}
```

By default, `AddCommands()` registers discovered command classes as **transient**.

### Manual registration (no scanning)

If you prefer explicit registration (also AOT-friendly), you can register commands one-by-one:

```csharp
using Microsoft.Extensions.DependencyInjection;
using TelegramBotKit.DependencyInjection;

builder.Services.AddMessageCommand<StartCommand>("/start", ServiceLifetime.Transient);
builder.Services.AddTextCommand<EchoCommand>(ignoreCase: true, ServiceLifetime.Transient, "hi", "hello");
builder.Services.AddCallbackCommand<LikeCommand>("like", ServiceLifetime.Transient);
```

---

## Default handlers (fallback)

If no command matches, TelegramBotKit calls default handlers:

- `IDefaultMessageHandler` for unmatched text messages
- `IDefaultCallbackHandler` for unmatched callback queries
- `IDefaultUpdateHandler` for update types that have no route mapping at all

By default they are **no-ops**.

To customize, register your own implementation:

```csharp
using TelegramBotKit.Fallbacks;

builder.Services.AddSingleton<IDefaultMessageHandler, MyDefaultMessageHandler>();
builder.Services.AddSingleton<IDefaultCallbackHandler, MyDefaultCallbackHandler>();
builder.Services.AddSingleton<IDefaultUpdateHandler, MyDefaultUpdateHandler>();
```
