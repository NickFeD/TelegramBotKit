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

[MessageCommand("/start")]
public sealed class StartCommand : IMessageCommand
{
    public Task HandleAsync(Message message, BotContext ctx)
    {
        // resolve dependencies via ctx.Services if needed
        // var myService = ctx.Services.GetRequiredService<MyService>();
        return Task.CompletedTask;
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

## Routing sugar (TelegramBotKit.Routing)

If you want Minimal-API-like registration without creating a command class, install `TelegramBotKit.Routing` and use `Use*` methods:

```csharp
using TelegramBotKit.Routing;

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

## Text commands

Text commands match exact message text (after trimming). You can register multiple triggers:

```csharp
bot.UseTextCommand(new[] { "hi", "hello" }, (msg, ctx) => /* ... */ Task.CompletedTask, ignoreCase: true);
```
