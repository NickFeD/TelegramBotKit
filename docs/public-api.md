# Public API map

This is a conceptual map of the supported public surface. IntelliSense XML
documentation is the detailed contract for individual members.

## Configuration

`AddTelegramBotKit(...)` registers the core runtime and returns a
`TelegramBotKitBuilder`. The builder configures global middleware, typed routes, and
the optional queued sender. `AddCommands()` registers attributed command classes;
explicit `AddMessageCommand`, `AddTextCommand`, and `AddCallbackCommand` methods are
available when scanning is undesirable.

Primary types:

- `TelegramBotKitServiceCollectionExtensions`
- `TelegramBotKitBuilder`
- `UpdateRouteBuilder<TPayload>`

## Global update pipeline

`IUpdateDispatcher` is the public update-processing entry point. Core preserves
an existing DI registration. Polling uses a singleton scheduler, so replacement
dispatchers must have a compatible lifetime and observe update cancellation.

`IScheduledUpdateDispatcher` is an optional public capability used alongside it.
It receives the host's execution continuation, allowing normal work to use
actor/DOP scheduling while active conversation responses can release a waiting
command. Decorators must forward this capability when they need to preserve it.
Completion covers the full update lifetime. See [Hosting](hosting.md) and
[webhook delivery contracts](webhooks.md) for cancellation and shutdown behavior.

`BotContext` contains the raw Telegram update, bot client, message sender,
per-update service provider, cancellation token, and an `Items` dictionary.

`IUpdateMiddleware` and `BotContextDelegate` define global middleware that can
surround routing for every update type or short-circuit it.

## Typed update routes

- `UpdateRoute<TPayload>` binds one `UpdateType` to a typed payload selector.
- `UpdateRoute.Create(...)` creates custom descriptors, including descriptors for
  future Telegram.Bot update types.
- `UpdateRoutes` provides descriptors for the update properties supported by the
  current Telegram.Bot dependency.
- `UpdateRouteBuilder<TPayload>` composes typed middleware and zero or one terminal.
- `UpdateRouteContext<TPayload>` exposes the selected payload and `BotContext`.
- `UpdateRouteDelegate<TPayload>` is the public typed continuation.
- `IUpdateRouteMiddleware<TPayload>` defines route-local nested middleware.
- `IUpdatePayloadHandler<TPayload>` defines the route's single terminal handler.

`.Use<TMiddleware>()` and inline `.Use(...)` are the primary typed route middleware
APIs. `.UseUpdateMiddleware(...)` explicitly adapts an existing BotContext-only
`IUpdateMiddleware`; it is a compatibility path rather than the preferred route API.

## Commands

Command contracts cover slash messages, exact text triggers, and callback data:

- `IMessageCommand`, `ITextCommand`, `ICallbackCommand`, and their common `ICommand`
  marker;
- `MessageCommandAttribute`, `TextCommandAttribute`, and
  `CallbackCommandAttribute`.

The optional source generator emits registrations used by `AddCommands()`. Its
public generated-code hooks are infrastructure and are not application extension
points.

## Messaging

`IMessageSender` exposes send, reply, edit, and callback-answer operations. Request
models include `SendText`, `SendPhoto`, `EditText`, `EditPhoto`, and
`AnswerCallback`. Extension methods derive identifiers from `Message` and
`CallbackQuery` payloads. Callback `Try*` methods safely report inline callbacks that
do not contain a message.

`QueuedMessageSenderOptions` configures the optional queued sender.

## Fallbacks

- `IDefaultMessageHandler` handles unmatched commands in the built-in Message route.
- `IDefaultCallbackHandler` handles unmatched callback commands.
- `IDefaultUpdateHandler` handles unregistered routes, routes without terminals, and
  null selected payloads.

Default implementations are no-ops and can be replaced through DI.

## Conversations

`WaitForUserResponse` supports one active in-memory wait per chat/user pair. It is a
small request/response helper rather than a general conversation state machine.

## Keyboards

`Keyboard` creates inline and reply keyboards, callback buttons, URL buttons, and
request buttons. `CallbackCommandKeyResolver` resolves generated callback keys for
typed buttons.

## Hosting

`TelegramBotKit.Hosting` provides `IUpdateReceiver`, `UpdateProcessingOptions`
and `AddTelegramBotKitUpdateProcessing()`. Admission is distinct from processing;
the default implementation uses in-memory queues and a shared host lifecycle.
`TelegramBotKit.Polling` provides `AddTelegramBotKitPolling()` and GetUpdates.
`TelegramBotKit.Webhook.AspNetCore` provides `AddTelegramBotKitWebhook()`,
`MapTelegramBotKitWebhook()`, `SetTelegramBotKitWebhookAsync()` and
`DeleteTelegramBotKitWebhookAsync()`. Remote management is explicit.
See [webhooks](webhooks.md) for contracts and the breaking package migration.

## Optional Routing package

The `TelegramBotKit.Routing` package provides delegate-based `UseMessageCommand`,
`UseTextCommand`, and `UseCallbackCommand` registration as an alternative to command
classes.

## Options

- `TelegramBotKitOptions`
- `PollingOptions`
- `WebhookOptions`
- `UpdateDeliveryMode`

Webhook reception is implemented by the optional ASP.NET Core package. These
options alone do not enable reception; register and map the adapter explicitly.

## Exceptions

`TelegramBotKitException` is the base library exception. Derived configuration,
registration, dispatch, and callback-data exceptions identify their corresponding
failure categories.

## Internal implementation

Core also bundles the compile-time command generator. `AddCommands()` applies
the existing generated registrations; explicit generic/fluent registration remains
available. Generated hooks are compiler infrastructure, not
application extension points. See [generated commands](generated-commands.md).

`Pipeline<TContext>`, `PipelineDelegate<TContext>`, `IPipelineNode<TContext>`,
`RouteRegistration<TPayload>`, and `UpdateHandlerRegistry` are implementation details.
They are not public extension points. Applications extend the runtime through the
public dispatcher, middleware, route, terminal, command, fallback, and messaging contracts above.
