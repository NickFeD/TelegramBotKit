# Update routes

[Docs index](README.md) | [Processing pipeline](processing-pipeline.md)

Routes are identified by Telegram `UpdateType`, never by the CLR payload alone.
`UpdateRoute<TPayload>` binds the update type, payload type and extraction function permanently.

## Register a terminal

```csharp
using Telegram.Bot.Types;
using TelegramBotKit;
using TelegramBotKit.DependencyInjection;
using TelegramBotKit.Dispatching;

var bot = services.AddTelegramBotKit(options => options.Token = token);
bot.Route(UpdateRoutes.Message).HandleWith<MessageHandler>();
bot.Route(UpdateRoutes.EditedMessage).HandleWith<EditedMessageHandler>();

public sealed class MessageHandler : IUpdatePayloadHandler<Message>
{
    public Task HandleAsync(Message payload, BotContext ctx) => Task.CompletedTask;
}

public sealed class EditedMessageHandler : IUpdatePayloadHandler<Message>
{
    public Task HandleAsync(Message payload, BotContext ctx) => Task.CompletedTask;
}
```

These routes share the `Message` payload type but have independent middleware and terminals.
`HandleWith<CallbackQueryHandler>()` on the Message route fails to compile when that handler implements only `IUpdatePayloadHandler<CallbackQuery>`.
Each route has at most one terminal. A second `HandleWith` call throws `TelegramBotKitRegistrationException` identifying the update type and existing/attempted handlers, even if both registrations use the same handler class.
DI registers/resolves the concrete handler type. Registering `IUpdatePayloadHandler<Message>` directly does not assign it to any route.

## Local middleware

```csharp
bot.Route(UpdateRoutes.EditedMessage)
   .Use<AuditMiddleware>() // implements IUpdateMiddleware
   .Use(async (ctx, next) =>
   {
       // before
       await next(ctx);
       // after
   })
   .HandleWith<EditedMessageHandler>();
```

Middleware uses the existing `IUpdateMiddleware` / `BotContextDelegate` contract.
First registered is outermost; `await next(ctx)` awaits downstream and then unwinds. Omitting `next` short-circuits. Exceptions propagate through local and global middleware.
Payload extraction occurs once before the local pipeline. Middleware receives `BotContext`; the terminal receives the typed payload.
Repeated `Route` calls with the **same descriptor instance** add to the same pipeline. Reuse custom descriptor instances as well.
A different descriptor for an already configured `UpdateType` is rejected to prevent silently changing its payload or extraction.

`Use<TMiddleware>(lifetime)` and `HandleWith<THandler>(lifetime)` default to scoped and respect pre-existing concrete DI registrations.
Global `UseMiddleware<T>()` also defaults to scoped; middleware resolves from the per-update scope, including constructor-injected scoped dependencies.
Complete configuration before resolving the dispatcher. Routes are frozen when the runtime registry is resolved.
Repeated `AddTelegramBotKit` calls on the same `IServiceCollection` share one builder/registry and global middleware list; option delegates accumulate. The first installed bot-client factory (or an existing client registration) remains in use.
Configure services on one thread. A rejected terminal/middleware registration does not modify route state; existing DI registrations and lifetimes remain respected.


## Built-in descriptors and defaults

`UpdateRoutes` provides all 23 typed payload routes in Telegram.Bot 22.9.0, including messages/edits/channel/business messages, callbacks, inline queries, payments, polls, membership, reactions and boosts.
The catalog describes routes; it does not automatically enable terminals for all of them.

When the application has not explicitly configured those routes, Message and CallbackQuery receive built-in terminals:

- Message: expected conversation response, then slash/text commands, then `IDefaultMessageHandler`.
- CallbackQuery: callback commands, then `IDefaultCallbackHandler`.

**Calling `Route(UpdateRoutes.Message)` or `Route(UpdateRoutes.CallbackQuery)` explicitly defines the entire route.** It opts out of that route's built-in command/conversation terminal, even when only adding middleware.
To keep default command behavior and add surrounding policies, use global middleware (with an update-type guard if needed).
Commands themselves remain a separate routing layer; command registration APIs are unchanged.
This ownership policy is enforced by core for both direct dispatch and polling. Hosting does not publish messages to conversation waiters independently.

An explicit route may have no terminal. Its local middleware runs and then invokes `IDefaultUpdateHandler` unless short-circuited.
Unregistered update types also invoke that fallback. An unexpectedly null extracted payload invokes update fallback directly, skipping the local pipeline but remaining inside global middleware.
All fallbacks are no-ops by default and update fallback is resolved from the per-update scope.

## Custom and future routes

A working custom descriptor using the current Telegram.Bot API:

```csharp
var edited = UpdateRoute.Create(
    UpdateType.EditedMessage,
    static update => update.EditedMessage);
// inferred: UpdateRoute<Message>
bot.Route(edited).HandleWith<EditedMessageHandler>();
```

Once an upgraded Telegram.Bot exposes a new update type and property, use the same pattern without a TelegramBotKit release (illustrative future names):

```csharp
var newFeature = UpdateRoute.Create(
    UpdateType.NewFeature,
    static update => update.NewFeature);
bot.Route(newFeature).HandleWith<NewFeatureHandler>();
```

There is no enum allowlist, reflection-based property guessing or string property name lookup in dispatch.

## Breaking migration

Previous versions configured the payload extractor and payload handler as two independent registrations. That payload-only dispatch model and its multiple-terminal behavior have no compatibility shim.
Replace the old two-step registration with one route registration:

```csharp
kit.Route(UpdateRoutes.InlineQuery).HandleWith<InlineQueryHandler>();
```

Replace extra handlers with local middleware and keep one terminal owner. Remove wrapper payloads or update-type guards used solely to distinguish Message from EditedMessage.
Applications that relied on an extra Message handler running after built-in commands must deliberately choose a custom terminal or move the additional behavior to middleware around the default flow.
The null-payload case now calls update fallback instead of silently returning.
