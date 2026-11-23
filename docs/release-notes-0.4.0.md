# What's new in TelegramBotKit 0.4.0

[Documentation](README.md) · [Migrating from 0.3.2](migration-0.3.2-to-0.4.0.md)

Changes in **0.4.0** compared with **0.3.2**. Runtime packages now support
**.NET 8, 9, 10 and 11**, with matching assets selected automatically by NuGet.
This release includes breaking API changes. Before upgrading an existing bot,
read the [migration guide](migration-0.3.2-to-0.4.0.md).

## Typed update routes

- Added `bot.Route(...)` to configure handling for a specific Telegram `UpdateType`.
  `Message` and `EditedMessage` have independent routes even though they share a payload type.
- Added built-in `UpdateRoutes` descriptors and custom descriptors through
  `UpdateRoute.Create(...)`.
- Each route supports its own middleware pipeline through `.Use<TMiddleware>()`
  and a single terminal handler through `.HandleWith<THandler>()`.
- Route configuration freezes when the dispatcher is first resolved from DI.
  Duplicate terminals and conflicting descriptors are reported as configuration errors.

Command bots still receive built-in Message and CallbackQuery routes automatically.
Explicitly configuring either through `Route(...)` replaces its built-in handling.
See [update routes](updates.md) and [middleware](middleware.md).

## Polling and ASP.NET Core webhooks

- Moved polling into the separate `TelegramBotKit.Polling` package.
  `AddTelegramBotKitPolling()` now belongs to the `TelegramBotKit.Polling` namespace.
- Added `TelegramBotKit.Webhook.AspNetCore`, with service registration through
  `AddTelegramBotKitWebhook(...)` and an HTTP endpoint through `MapTelegramBotKitWebhook()`.
- Webhook delivery validates the secret in the `X-Telegram-Bot-Api-Secret-Token` header.
  Registering and deleting the webhook in Telegram are explicit operations through
  `SetTelegramBotKitWebhookAsync()` and `DeleteTelegramBotKitWebhookAsync()`.
- Both transports use the shared admission and execution infrastructure in Hosting:
  `IUpdateReceiver`, the scheduler and `UpdateProcessingOptions`.

Webhook acknowledgements and polling offset advancement indicate update admission,
not handler completion. The queue remains in memory; durable storage and duplicate
suppression are not provided.
See [Hosting](hosting.md) and [webhooks](webhooks.md).

## Command generation bundled with Core

- The command generator ships with `TelegramBotKit` as a compiler analyzer.
  A separate `TelegramBotKit.Generators` NuGet reference is no longer required.
- The generator targets Roslyn 4.8 so applications can use .NET SDK 8 and later.
- `services.AddCommands()` uses registrations generated at compile time.
  Runtime command discovery through reflection has been removed.
- Typed callback buttons use generated mappings between handler classes and keys.
  Explicit command and callback-key registration remains available.
- Fixed escaping of quotes, backslashes and control characters in generated strings.
- Fixed detection of conflicting text triggers with different case-comparison policies,
  regardless of declaration order. Added links to diagnostic documentation.

Generated hooks retain their existing limitation across multiple assemblies:
the last registration replaces the previous one.
See [generated commands](generated-commands.md).

## Execution, conversations and messaging

- Global middleware registered by class now defaults to a scoped lifetime and is
  resolved from the current update's scope.
- Added a separate scheduling path for `WaitForUserResponse` replies, allowing a
  reply to arrive while the waiting command occupies an actor or the only concurrency slot.
- Strengthened Hosting shutdown: accepted work is canceled and awaited before its
  dependencies are disposed.
- Text and photo sends based on `Message` or `CallbackQuery`, including replies,
  inherit `MessageThreadId` unless another thread is explicitly supplied.
- Fixed caller cancellation for queued sends waiting behind an active request:
  the caller no longer has to wait for the head of the queue to finish.
- Fixed retries for network failures wrapped by Telegram.Bot in `RequestException`,
  preserving the attempt limit and final error.

See [conversations](conversations.md), [messaging](messaging.md)
and [conversation response scheduling](hosting.md#conversation-response-scheduling).

## Samples, documentation and release validation

- Updated the polling samples and added an ASP.NET Core webhook sample.
  ConsolePolling now has documented [manual verification scenarios](samples/console-polling.md).
- Polling samples support an ignored `appsettings.Local.json` file.
  Environment variables and command-line arguments take precedence over JSON configuration.
  Missing tokens produce a readable configuration error.
- Expanded public API XML documentation and added guides for migration, routes,
  command generation and webhooks.
- Added component [benchmarks](benchmarks.md) that run without contacting Telegram.
  Their presence does not imply that every scenario is faster.
- Release validation runs both test projects, checks configuration startup for all
  four samples, validates package contents and verifies command generation in NuGet
  consumers with direct and transitive Core dependencies. Library tests and packed
  consumers run on .NET 8, 9, 10 and 11.
- Updated SourceLink and the native SQLite dependency in PhotoSelector.
  The repository build SDK is pinned to 11.0.100 to build all four target frameworks.
  Samples and benchmarks continue to target .NET 10.

## Upgrading

`Map<TPayload>()` and `AddUpdateHandler<TPayload, THandler>()` have been replaced
by typed routes. Polling has moved out of Hosting, and command discovery no longer
has a reflection fallback. Rebuild applications and review middleware lifetimes.
The [migration guide](migration-0.3.2-to-0.4.0.md) provides step-by-step replacements.

Removing reflection-based command discovery does not establish Native AOT compatibility.
That mode requires separate publish and execution checks.
