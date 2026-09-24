# TelegramBotKit - Project Context

> Current state: 2026-09-23. CURRENT describes implemented behavior; PLANNED describes separate future work.

## Purpose and stack - CURRENT

TelegramBotKit is a lightweight .NET 10 toolkit for Telegram bots, using Telegram.Bot, Microsoft DI/Generic Host and optional Roslyn command generation.
It provides update pipelines, typed handlers, commands, messaging and simple request/response conversations rather than an application framework or event bus.

## Packages - CURRENT

- TelegramBotKit: core contracts and runtime.
- TelegramBotKit.Hosting: optional polling delivery and scheduling.
- TelegramBotKit.Routing: optional fluent command registration.
- TelegramBotKit.Generators: optional compile-time command registration.

Optional packages extend core; core does not depend on them.

## Update processing - CURRENT

`UpdateRoute<TPayload>` permanently binds `UpdateType`, payload type and selector.
`bot.Route(descriptor).Use<TMiddleware>().HandleWith<THandler>()` retains generic type safety.
The internal registry is keyed by `UpdateType`; routes sharing a CLR payload remain isolated.
Each route owns 0..N middleware components and 0..1 terminal. Duplicate terminals fail during configuration.
`IUpdatePayloadHandler<TPayload>` remains the typed public contract; DI constructs concrete handlers rather than choosing route ownership.
`UpdateRoutes` covers known Telegram payloads; `UpdateRoute.Create(...)` supports custom future routes.

Global middleware surrounds update routing; local middleware belongs to one route. Both use nested next/unwind semantics and can short-circuit.
Instances are resolved from the per-update scope, disposed after processing and unwind.
Missing routes, absent terminals and null selected payloads invoke update fallback.

## Commands and conversations - CURRENT

Message and callback command processing remain defaults for routes not explicitly configured by the application.
Explicit route configuration owns the whole route, including middleware-only routes that end in update fallback.
Commands remain a separate application routing layer. Expected conversation responses still precede commands in the default message route.
The conversation helper supports linear request/response waits, not a conversation state machine.

## Messaging and hosting - CURRENT

`IMessageSender` is the Telegram messaging facade, with optional queued delivery.
Polling is the main delivery path; scheduling serializes related updates while allowing unrelated updates to run concurrently.

## PLANNED

Webhook delivery remains separate and incomplete. The update-route registration redesign is implemented; terminal multi-handler chains are removed.

## Related documents

- ARCHITECTURE.md: runtime and subsystem boundaries.
- DECISIONS.md: current, replaced and rejected decisions.
- CURRENT_STATE.md: handoff and validation.
- OPEN_QUESTIONS.md: resolved routing questions.
- docs/updates.md: public API and migration.
