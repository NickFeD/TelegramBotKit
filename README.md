# TelegramBotKit

TelegramBotKit is a lightweight toolkit for building Telegram bots on .NET with a structured update pipeline, typed handlers, and simple command routing.

## Features

- Middleware pipeline for update processing.
- Typed update payload handlers (`IUpdatePayloadHandler<TPayload>`).
- Message/text/callback commands with attributes.
- `WaitForUserResponse` helper for request/response flows.
- `IMessageSender` facade for sending messages.
- Optional queued sender to reduce rate-limit errors.

## Installation

```bash
dotnet add package TelegramBotKit
dotnet add package TelegramBotKit.Hosting
```

## Quick start (polling)

1) Add configuration.

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

2) Create a host.

```csharp
using System.Reflection;
using Microsoft.Extensions.Hosting;
using TelegramBotKit.DependencyInjection;
using TelegramBotKit.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

var bot = builder.Services.AddTelegramBotKit(opt =>
{
    builder.Configuration.GetSection("TelegramBotKit").Bind(opt);
});

builder.Services.AddCommandsFromAssemblies(Assembly.GetExecutingAssembly());

bot.UseQueuedMessageSender();

builder.Services.AddTelegramBotKitPolling();

var host = builder.Build();
await host.RunAsync();
```

3) Add a command.

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

## Running the sample

```bash
dotnet run --project samples/TelegramBotKit.Sample.ConsolePolling
```
