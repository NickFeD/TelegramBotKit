# TelegramBotKit

TelegramBotKit is a .NET 10 toolkit for building Telegram bots with polling hosting,
commands, strongly typed update routes, and nested middleware.

Its core features include:

- polling through `Microsoft.Extensions.Hosting`;
- global middleware for update-wide policies;
- strongly typed `UpdateType` routes and route-local middleware;
- one typed terminal handler per route;
- message, text, and callback commands;
- simple request/response conversations;
- messaging and keyboard helpers;
- an optional queued sender;
- an optional command source generator;
- custom descriptors for future Telegram update types.

See the [documentation index](docs/README.md) for feature guides and the
[Quick Start](docs/quickstart.md) for the smallest working bot.

## Packages

- `TelegramBotKit` — core configuration, commands, routes, middleware, and messaging.
- `TelegramBotKit.Hosting` — polling and hosted-service integration.
- `TelegramBotKit.Routing` — optional delegate-based command registration.
- `TelegramBotKit.Generators` — optional compile-time command discovery.

## Minimal polling bot

```csharp
using Microsoft.Extensions.Hosting;
using TelegramBotKit.DependencyInjection;
using TelegramBotKit.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddTelegramBotKit(options =>
    builder.Configuration.GetSection("TelegramBotKit").Bind(options));
builder.Services.AddCommands();
builder.Services.AddTelegramBotKitPolling();

await builder.Build().RunAsync();
```

An attributed command is enough to handle `/start`:

```csharp
using Telegram.Bot.Types;
using TelegramBotKit.Commands;
using TelegramBotKit.Messaging;

[MessageCommand("/start")]
public sealed class StartCommand : IMessageCommand
{
    public Task HandleAsync(Message message, BotContext context) =>
        context.Sender.SendText(
            message.Chat.Id,
            new SendText { Text = "Hello." },
            context.CancellationToken);
}
```

Normal command bots do not configure `UpdateRoutes.Message` or
`UpdateRoutes.CallbackQuery`; TelegramBotKit installs their command-processing routes
automatically. Use `Route(...)` to handle another Telegram update type, add typed
route-local middleware, or deliberately replace a built-in route.

## Samples

- `TelegramBotKit.Sample.MinimalPolling` contains only configuration, one `/start`
  command, and polling.
- `TelegramBotKit.Sample.ConsolePolling` demonstrates commands, fallbacks, global and
  typed route middleware, an edited-message route, keyboards, conversations, and the
  queued sender.

```bash
dotnet run --project samples/TelegramBotKit.Sample.MinimalPolling
```

## Documentation

- [Quick Start](docs/quickstart.md)
- [Commands](docs/commands-and-routing.md)
- [Typed update routes](docs/updates.md)
- [Middleware](docs/middleware.md)
- [Messaging](docs/messaging.md)
- [Hosting](docs/hosting.md)
- [Conversations](docs/conversations.md)
- [Keyboards](docs/keyboards.md)
- [Public API map](docs/public-api.md)
