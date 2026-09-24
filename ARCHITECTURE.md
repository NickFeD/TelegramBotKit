# TelegramBotKit — Architecture

> Актуальное архитектурное состояние на 2026-09-24.
>
> Этот файл описывает слои, зависимости и уже установленные архитектурные границы. Незакрытые варианты не дублируются здесь подробно — они находятся в `OPEN_QUESTIONS.md`.

## 1. Пакетная структура

### CURRENT

`TelegramBotKit` — базовый пакет с core contracts и runtime pipeline.

Опциональные расширения:

- `TelegramBotKit.Hosting` — update delivery и scheduling;
- `TelegramBotKit.Routing` — удобный routing API поверх core;
- `TelegramBotKit.Generators` — compile-time registration.

Архитектурное правило: optional packages расширяют core, но core не должен зависеть от них.

## 2. Runtime pipeline

### CURRENT

Высокоуровневый поток:

```text
Telegram Update
  -> delivery/hosting
  -> dispatcher
  -> per-update context/scope
  -> global middleware
  -> route by UpdateType
  -> descriptor payload extraction
  -> route-local middleware
  -> 0..1 terminal or fallback
  -> pipeline unwind
  -> dispose per-update scope
```

Middleware оборачивает routing и downstream processing.

Pipeline composition is implemented once by the domain-independent internal
`Pipeline<TContext>` kernel. It owns ordered node composition, nested `next`
semantics and terminal delegate construction; it has no Telegram, routing, DI or
scope responsibilities.

The global path adapts `IUpdateMiddleware` to `IPipelineNode<BotContext>`. Each
route compiles a `Pipeline<UpdateRouteContext<TPayload>>` when the registry is
frozen. The internal route context carries the already extracted payload together
with the current `BotContext`; the typed handler or update fallback is the terminal.
`IUpdateMiddleware` and `IUpdatePayloadHandler<TPayload>` remain public
TelegramBotKit façades rather than pipeline-kernel contracts.

## 3. Middleware

### CURRENT

Основной контракт — `IUpdateMiddleware`.

Middleware предназначен для cross-cutting concerns:

- logging;
- metrics;
- authorization/policies;
- throttling;
- exception boundaries;
- другие сквозные политики.

Middleware может short-circuit pipeline.

Архитектурное правило: middleware не является заменой type-specific update handlers.

## 4. Update routing and handlers

### CURRENT

`UpdateRoute<TPayload>` binds `UpdateType`, payload type and a strongly typed `Update -> TPayload?` selector.
`UpdateRoutes` contains descriptors for all 23 typed update properties in Telegram.Bot 22.9.0.
Applications can create descriptors with `UpdateRoute.Create(type, selector)` without catalog changes or reflection.

The internal `UpdateHandlerRegistry` owns route registrations keyed by `UpdateType`.
Each registration has a descriptor, additive middleware and a nullable single terminal descriptor.
`HandleWith<THandler>()` requires `IUpdatePayloadHandler<TPayload>` at compile time; a second terminal fails during configuration with both handler names.
Repeated `Route` calls with the same descriptor compose the same route. A different descriptor for an occupied `UpdateType` is rejected, including a different extractor for the same payload type.

Runtime selection uses `Update.Type`, extracts the payload once, then enters the local pipeline.
Message and EditedMessage can both carry `Message` without sharing terminals or middleware.
DI constructs concrete handler/middleware types with their configured lifetimes; service enumeration and DI registration order do not determine route ownership.
Global and route middleware use `IUpdateMiddleware` and nested `next(ctx)` semantics, including short-circuiting and exception propagation.
Class middleware is resolved from the per-update scope. Scope disposal is asynchronous and occurs after unwind, including failures.
Configuration ends when the registry is resolved. Freeze snapshots and compiles each route pipeline; subsequent route mutations are rejected.
Repeated `AddTelegramBotKit` calls on one service collection return the same builder/configuration state; option delegates accumulate, while runtime registrations are installed once.
Terminal/middleware configuration validates ownership and mutability, registers the concrete DI dependency, then commits the route change. A DI registration failure leaves route state unchanged. Configuration is single-threaded, like the underlying service collection.


Message and CallbackQuery receive built-in command-processing terminals only when those routes were not explicitly configured.
Explicit route configuration defines the entire route; a middleware-only route ends in `IDefaultUpdateHandler`.
Missing routes, absent terminals and unexpectedly null selected payloads use the same update fallback resolved from the update scope.
Null payloads bypass route middleware; global middleware still surrounds fallback. This intentionally replaces the previous silent null-payload return.

Conversation publication is decided only by the core dispatcher, after global middleware, and only when the registry identifies the built-in Message terminal. D-020 is unchanged: explicit Message routes never implicitly publish to a waiter.
Hosting supplies an internal scheduling continuation, not routing policy. Normal updates run their complete pipeline inside actor/DOP scheduling. A potential response to an already active waiter runs in its own scope outside the occupied actor/DOP slot, through the same global middleware; this allows an awaiting command to resume even with DOP=1.
The waiter is rechecked after middleware. If it expired or was consumed, only the remaining routing continuation is scheduled; middleware runs once and the same scope remains alive until unwind completes.


## 5. Commands

### CURRENT

Commands — отдельный слой внутри соответствующей update processing ветки.

Архитектурное правило:

```text
update-type routing != command routing
```

Примеры `/start`, text commands и callback commands не должны использоваться как замена обсуждению структуры handlers по Telegram `UpdateType`.

## 6. Context и dependency boundaries

### CURRENT

Для каждого update формируется собственный runtime context и DI scope.

Public contracts должны оставаться компактными и устойчивыми.

Internal registry, node и router types не обязаны становиться частью public API только потому, что используются новой реализацией pipeline.

## 7. Hosting и concurrency

### CURRENT

Основной delivery mode — polling.

Hosting допускает конкурентную обработку updates, при этом связанные события могут сериализоваться, чтобы снизить race conditions.

Точная внутренняя scheduler implementation не является частью публичного архитектурного контракта.

## 8. Messaging и conversations

### CURRENT

`IMessageSender` — фасад Telegram messaging operations.

Conversation helper поддерживает простые линейные request/response сценарии и не заменяет полноценную state machine.

## 9. Webhook delivery

### PLANNED

Полноценная webhook support остаётся отдельным направлением развития. Наличие связанных option types само по себе не означает завершённую поддержку.

## 10. Routing decisions

Update-route registration and multi-handler control flow are resolved; see `OPEN_QUESTIONS.md` and D-018 through D-020 in `DECISIONS.md`.
