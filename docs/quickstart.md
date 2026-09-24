# Quick Start

[Docs index](README.md) · Next: [Commands](commands-and-routing.md)

This guide builds the smallest polling bot with one `/start` command.

## 1. Create the project

```bash
dotnet new console -n MyBot -f net10.0
cd MyBot
dotnet add package TelegramBotKit
dotnet add package TelegramBotKit.Hosting
```

`TelegramBotKit.Generators` is optional. Without it, `AddCommands()` discovers
attributed commands through its reflection fallback.

## 2. Add configuration

Create `appsettings.json`:

```json
{
  "TelegramBotKit": {
    "Token": "PUT_YOUR_BOT_TOKEN_HERE"
  }
}
```

Ensure the file is copied to the output directory:

```xml
<ItemGroup>
  <None Update="appsettings.json">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

## 3. Configure and run the host

Replace `Program.cs` with:

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using TelegramBotKit.DependencyInjection;
using TelegramBotKit.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", optional: false);

var bot = builder.Services.AddTelegramBotKit(options =>
    builder.Configuration.GetSection("TelegramBotKit").Bind(options));
builder.Services.AddCommands();
builder.Services.AddTelegramBotKitPolling();

await builder.Build().RunAsync();
```

## 4. Add `/start`

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

Run the bot:

```bash
dotnet run
```

## You usually do not need `Route(...)`

Normal command bots do not configure `UpdateRoutes.Message` or
`UpdateRoutes.CallbackQuery`. TelegramBotKit installs built-in routes that handle
message, text, and callback commands automatically.

Use `Route(...)` when you want to:

- handle another Telegram `UpdateType`;
- add typed middleware to one route;
- deliberately replace the built-in Message or CallbackQuery route.

For example, handle edited messages with a typed terminal:

```csharp
using Telegram.Bot.Types;
using TelegramBotKit.Dispatching;

bot.Route(UpdateRoutes.EditedMessage)
   .HandleWith<EditedMessageHandler>();

public sealed class EditedMessageHandler : IUpdatePayloadHandler<Message>
{
    public Task HandleAsync(Message message, BotContext context) =>
        Task.CompletedTask;
}
```

Explicitly configuring Message or CallbackQuery replaces that route's built-in
command terminal. See [typed update routes](updates.md) before doing so.

## Optional queued sender

Queued sending is useful for rate limiting, but it is not required for a working bot:

```csharp
bot.UseQueuedMessageSender(options =>
{
    options.GlobalMaxPerSecond = 25;
    options.PerChatMinDelay = TimeSpan.FromSeconds(1);
});
```

Call it after `AddTelegramBotKit` and before building the host.

## Optional custom `HttpClient`

`AddTelegramBotKit` also accepts either a ready `HttpClient` or a factory:

```csharp
builder.Services.AddTelegramBotKit(
    options => builder.Configuration.GetSection("TelegramBotKit").Bind(options),
    services => services.GetRequiredService<IHttpClientFactory>()
        .CreateClient("TelegramBotKit"));
```

This is useful for proxies, custom handlers, and centralized timeout management.

## Next

- [Commands](commands-and-routing.md)
- [Typed update routes](updates.md)
- [Middleware](middleware.md)
- [Hosting](hosting.md)
