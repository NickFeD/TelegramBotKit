# Typed update routes

[Docs index](README.md) · [Processing pipeline](processing-pipeline.md) · [Middleware](middleware.md)

Routes are identified by Telegram `UpdateType`. `UpdateRoute<TPayload>` permanently
binds that identity to the selector that extracts its typed payload.

## Command bots already have Message and CallbackQuery routes

Normal command bots do not call `Route(UpdateRoutes.Message)` or
`Route(UpdateRoutes.CallbackQuery)`. TelegramBotKit installs built-in terminals for
those routes when the application has not configured them:

- Message: conversations, slash/text commands, then `IDefaultMessageHandler`;
- CallbackQuery: callback commands, then `IDefaultCallbackHandler`.

Use `Route(...)` when handling another update type, adding typed route-local
middleware, or deliberately replacing a built-in route.

Calling `Route(UpdateRoutes.Message)` or `Route(UpdateRoutes.CallbackQuery)` gives the
application complete ownership and disables that route's built-in terminal. Use
global middleware when you only need to surround the existing command flow.

## A typed terminal

```csharp
using Telegram.Bot.Types;
using TelegramBotKit.Dispatching;

bot.Route(UpdateRoutes.EditedMessage)
   .HandleWith<EditedMessageHandler>();

public sealed class EditedMessageHandler : IUpdatePayloadHandler<Message>
{
    public Task HandleAsync(Message message, BotContext context)
    {
        // Final owner of the EditedMessage route.
        return Task.CompletedTask;
    }
}
```

A route owns zero or one terminal. A second `HandleWith` call is a configuration
error. Message and EditedMessage both carry `Message`, but remain independent because
the registry key is `UpdateType`.

## Typed route middleware

Typed route middleware receives the exact payload extracted for that route:

```csharp
public sealed class EditedMessageAudit : IUpdateRouteMiddleware<Message>
{
    public async Task InvokeAsync(
        UpdateRouteContext<Message> context,
        UpdateRouteDelegate<Message> next)
    {
        Message message = context.Payload;

        // Route-specific behavior before the terminal.
        await next(context);
        // Route-specific behavior after the terminal.
    }
}
```

Register it before the terminal:

```csharp
bot.Route(UpdateRoutes.EditedMessage)
   .Use<EditedMessageAudit>()
   .HandleWith<EditedMessageHandler>();
```

`Use<TMiddleware>()` requires `IUpdateRouteMiddleware<TPayload>`, so middleware for
`CallbackQuery` cannot be attached to `UpdateRoute<Message>`.

Inline middleware is typed as well:

```csharp
bot.Route(UpdateRoutes.EditedMessage)
   .Use(async (context, next) =>
   {
       var message = context.Payload;
       await next(context);
   })
   .HandleWith<EditedMessageHandler>();
```

The first registered middleware is outermost. Omitting `next(context)` short-circuits
the route; exceptions unwind naturally through typed route and global middleware.
Payload extraction occurs once before route middleware.

## Compatibility with `IUpdateMiddleware`

`IUpdateMiddleware` is the global, `BotContext`-only contract. Existing components
using it can be adapted to one route explicitly:

```csharp
bot.Route(UpdateRoutes.EditedMessage)
   .UseUpdateMiddleware<LegacyAuditMiddleware>()
   .HandleWith<EditedMessageHandler>();
```

The inline compatibility overload is also named `UseUpdateMiddleware(...)`. Prefer
typed `.Use(...)` for new route-local middleware.

## Built-in descriptors and fallback

`UpdateRoutes` contains descriptors for every typed update property supported by the
current Telegram.Bot dependency. The catalog does not automatically install handlers
for every route.

An explicit route without a terminal runs its middleware and then
`IDefaultUpdateHandler`. Unregistered update types use the same fallback. If a payload
selector returns null, route middleware is skipped and fallback runs inside the global
pipeline.

Class middleware and terminals default to scoped DI lifetimes and respect an existing
concrete registration. Complete route configuration before resolving the dispatcher;
the registry freezes and compiles route pipelines at that point.

## Custom and future routes

Create a descriptor once and reuse that instance when composing its route:

```csharp
var edited = UpdateRoute.Create(
    UpdateType.EditedMessage,
    static update => update.EditedMessage);

bot.Route(edited)
   .Use<EditedMessageAudit>()
   .HandleWith<EditedMessageHandler>();
```

The same API supports future Telegram.Bot update types without a TelegramBotKit
catalog update:

```csharp
var future = UpdateRoute.Create(
    UpdateType.SomeFutureType,
    static update => update.SomeFuturePayload);

bot.Route(future)
   .Use<FutureMiddleware>()
   .HandleWith<FutureHandler>();
```

There is no built-in allowlist. A different descriptor cannot replace an already
configured `UpdateType`, because that would silently change payload extraction.
