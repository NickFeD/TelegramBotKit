# TelegramBotKit — Architectural Decisions

> Только уже принятые архитектурные решения, а также важные `REPLACED`/`REJECTED` решения. Открытые развилки находятся в `OPEN_QUESTIONS.md`.

## D-001 — Update processing строится как pipeline

**Status: CURRENT**

Updates проходят единый runtime pipeline:

```text
dispatcher -> per-update context/scope -> middleware -> update routing -> handler/fallback
```

---

## D-002 — `UpdateType` является первой осью routing

**Status: CURRENT**

Сначала определяется Telegram `UpdateType`, затем выполняется typed processing соответствующего route.

---

## D-003 - Payload-only handler identity

**Status: REPLACED**

Payload-only handler resolution has been removed. Different UpdateTypes sharing a CLR payload now have independent route ownership.

---

## D-004 - Additive terminal-handler registration

**Status: REPLACED by D-018**

The former rule allowed multiple additive terminal handlers. Routes now have at most one terminal; additive composition belongs to middleware.

---

## D-005 — `IUpdatePayloadHandler<TPayload>` сохраняется как typed façade

**Status: CURRENT design intent**

`IUpdatePayloadHandler<TPayload>` остаётся читаемым и типобезопасным пользовательским контрактом.

Internal registry/node model может быть переработана без требования строить всю маршрутизацию вокруг CLR-типа `TPayload`.

---

## D-006 — Update handlers и commands — разные уровни

**Status: CURRENT**

Update handlers относятся к обработке Telegram update categories.

Message/text/callback commands — прикладной routing более низкого уровня.

Command routing не является архитектурной заменой update-type handlers.

---

## D-007 — Middleware предназначен для cross-cutting concerns

**Status: CURRENT**

Middleware оборачивает downstream pipeline и может short-circuit обработку.

Он предназначен для сквозных политик и не отменяет type-specific update handlers.

---

## D-009 - Routes sharing a CLR payload are isolated

**Status: CURRENT**

`UpdateType` is the registry key. Message and EditedMessage own independent pipelines and terminals even though both carry `Message`.

---

## D-010 - Sequential multi-handler execution

**Status: REPLACED by D-018**

Terminal chains have been removed. Ordering, short-circuit and unwind belong to nested middleware; there is no second handler control-flow mechanism.

---

## D-011 — Middleware использует nested pipeline semantics

**Status: CURRENT**

Middleware выполняется как вложенная цепочка; невызванный downstream continuation останавливает дальнейшую обработку.

---

## D-012 — Conversation helper обрабатывается до command routing

**Status: CURRENT**

Ожидаемый пользовательский ответ имеет приоритет перед обычным command routing в соответствующем message flow.

---

## D-013 — Conversation helper не допускает неоднозначную доставку ответа

**Status: CURRENT**

Для одного логического пользователя/чата не должно существовать несколько конкурирующих ожиданий одного и того же следующего ответа.

---

## D-014 — Hosting ограничивает race conditions для связанных updates

**Status: CURRENT**

Связанные updates могут сериализоваться, при этом независимые updates не обязаны обрабатываться глобально последовательно.

---

## D-015 — Command registration должна иметь AOT-friendly path

**Status: CURRENT**

Compile-time registration через generator является поддерживаемым путём; reflection не должна быть единственной обязательной моделью discovery.

---

## D-016 — Internal pipeline model не обязана быть public API

**Status: CURRENT**

Registry/node/router implementation details могут меняться без расширения публичного контракта библиотеки.

---

## D-017 — Webhook support является отдельным направлением

**Status: PLANNED**

Основной текущий delivery path — polling. Webhook support развивается отдельно и не считается завершённой только из-за наличия связанных option types.

---

## D-018 - One terminal, additive route pipeline

**Status: CURRENT**

Each `UpdateType` route owns 0..N middleware components and 0..1 terminal.
A second terminal is a registration error identifying the route and both handlers.
No replacement, DI-order selection, fan-out, priorities or Handled/Continue results are supported.

## D-019 - Typed descriptors and forward compatibility

**Status: CURRENT**

`UpdateRoute<TPayload>` binds update identity, payload type and extraction permanently.
`Route(descriptor)` retains `TPayload`; `HandleWith<THandler>()` requires `IUpdatePayloadHandler<TPayload>`.
Built-in `UpdateRoutes` are conveniences, not a closed routing switch. Custom descriptors use `UpdateRoute.Create(type, selector)`.
Reusing a descriptor composes one route; conflicting descriptors for the same `UpdateType` fail.

## D-020 - Defaults and scope ownership

**Status: CURRENT**

Built-in message/callback terminals are installed only for routes not explicitly configured.
Explicit routes own their complete processing; empty or middleware-only routes end in update fallback.
Missing routes and null payloads also invoke update fallback. Global and local middleware resolve from the update scope, which is disposed after unwind.

## D-021 - Internal generic pipeline kernel

**Status: CURRENT**

Pipeline composition is implemented once by the domain-independent internal
`Pipeline<TContext>` primitive. It owns ordered node composition, nested `next`
semantics and terminal delegate construction without depending on TelegramBotKit
domain types.

Global update middleware executes through `Pipeline<BotContext>`. Route-local
processing extracts its typed payload once and executes through
`Pipeline<UpdateRouteContext<TPayload>>`; the route pipeline is compiled when the
registry freezes.

`IUpdateMiddleware` and `IUpdatePayloadHandler<TPayload>` remain public
TelegramBotKit façades adapted to the internal node and terminal model. The kernel,
its nodes and the typed route execution context remain internal API.

## Registration and delivery implementation notes

D-018 through D-021 remain in effect. A single service collection owns one shared builder/registry across all `AddTelegramBotKit` calls. Failed DI registration does not commit terminal/middleware changes.
Core dispatcher checks conversation ownership from the registry after global middleware for both direct and polling delivery. Hosting only schedules execution. Active built-in conversation replies can bypass the occupied actor/DOP slot to unblock the waiting command; stale waiter candidates return to scheduled routing without rerunning middleware.

## REPLACED

- Payload-only handler identity.
- Additive terminal handlers and sequential multi-handler chains (D-004, D-010).
- Silent return on a null extracted payload.

## REJECTED

- Silently replacing a configured terminal or selecting it by DI registration order.
- Event bus, parallel terminal fan-out, handler priorities and Handled/Continue chains.
- Command routing as a replacement for update-type dispatch.
