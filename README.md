# TelegramBotKit

TelegramBotKit is a lightweight toolkit for building Telegram bots on .NET with a structured update pipeline, typed handlers, and simple command routing.

📚 **Start here:** **[Documentation index](docs/README.md)**

## Packages

- `TelegramBotKit` — core pipeline, dispatching, commands, messaging helpers.
- `TelegramBotKit.Hosting` — polling/hosting integration.
- `TelegramBotKit.Routing` — optional ASP.NET-style `Use*` routing sugar.
- `TelegramBotKit.Generators` — optional Roslyn source generator for compile-time `AddCommands()` registration.

## Features

- Middleware pipeline for update processing.
- Typed update payload handlers (`IUpdatePayloadHandler<TPayload>`).
- Message/text/callback commands (attributes + optional routing sugar).
- `WaitForUserResponse` helper for request/response flows.
- `IMessageSender` facade for sending messages.
- Optional queued sender to reduce rate-limit errors.

## Requirements

- **.NET 10** (current target framework is `net10.0`).

## Installation

```bash
dotnet add package TelegramBotKit
dotnet add package TelegramBotKit.Hosting

# optional
dotnet add package TelegramBotKit.Routing

# optional (compile-time AddCommands)
dotnet add package TelegramBotKit.Generators
````

## Quick start (polling)

1. Add configuration.

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

> `AllowedUpdates: []` means “allow all update types”.
> If you want only specific types, list them explicitly.

2. Create a host.

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

// Optional: queued sender (helps with Telegram rate limits)
bot.UseQueuedMessageSender();

builder.Services.AddTelegramBotKitPolling();

var host = builder.Build();
await host.RunAsync();
```

3. Add a command.

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

## Documentation

* **[Documentation index](docs/README.md)** (recommended starting point)
* [Quickstart](docs/quickstart.md)
* [Commands and routing](docs/commands-and-routing.md)
* [Updates and payload handlers (custom Update types)](docs/updates.md)
* [Middleware](docs/middleware.md)
* [Hosting / Polling](docs/hosting.md)
* [Conversations (`WaitForUserResponse`)](docs/conversations.md)
* [Keyboards](docs/keyboards.md)
* [Public API notes](docs/public-api.md)
* [Releasing](docs/releasing.md)

## Running the sample

```bash
dotnet run --project samples/TelegramBotKit.Sample.ConsolePolling
```
