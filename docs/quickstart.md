# Quick start

This guide shows a minimal polling bot using **TelegramBotKit**.

## Requirements

- **.NET 10** (TelegramBotKit currently targets `net10.0`).

## 1) Install packages

```bash
dotnet add package TelegramBotKit
dotnet add package TelegramBotKit.Hosting
# optional
dotnet add package TelegramBotKit.Routing
# optional (compile-time AddCommands)
dotnet add package TelegramBotKit.Generators
```

## 2) Configure

Create `appsettings.json`:

```json
{
  "TelegramBotKit": {
    "Token": "PUT_YOUR_BOT_TOKEN_HERE",
    "Polling": {
      "MaxDegreeOfParallelism": 4,
      "Limit": 100,
      "TimeoutSeconds": 10,
      "AllowedUpdates": []
    }
  }
}
```

Notes:

- `AllowedUpdates: []` means **all update types** (Telegram default). If you set it to a non-empty list, Telegram will only deliver the types you requested.

## 3) Create a host

```csharp
using Microsoft.Extensions.Hosting;
using TelegramBotKit.DependencyInjection;
using TelegramBotKit.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

var bot = builder.Services.AddTelegramBotKit(opt =>
{
    builder.Configuration.GetSection("TelegramBotKit").Bind(opt);
});

// Registers attributed commands.
// If TelegramBotKit.Generators is installed, this is compile-time.
// Otherwise it falls back to reflection-based discovery.
builder.Services.AddCommands();

bot.UseQueuedMessageSender();

builder.Services.AddTelegramBotKitPolling();

var host = builder.Build();
await host.RunAsync();
```

## 4) Add a command

```csharp
using Telegram.Bot.Types;
using TelegramBotKit.Commands;
using TelegramBotKit.Messaging;

namespace MyBot.Commands;

[MessageCommand("/start")]
public sealed class StartCommand : IMessageCommand
{
    public Task HandleAsync(Message message, BotContext ctx)
    {
        return ctx.Sender.SendText(message.Chat.Id, new SendText
        {
            Text = "Hello."
        }, ctx.CancellationToken);
    }
}
```

## Next

- Commands and routing: `./commands-and-routing.md`
- Middleware: `./middleware.md`
- Hosting: `./hosting.md`
- Updates and payload handlers: `./updates.md`
- Conversations (WaitForUserResponse): `./conversations.md`
- Keyboards: `./keyboards.md`
