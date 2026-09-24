# Open Questions

There are no remaining open questions from the update-routing redesign.

## Resolved - typed route middleware

`UpdateRoute<TPayload>` now propagates its payload type through
`IUpdateRouteMiddleware<TPayload>`, the read-only `UpdateRouteContext<TPayload>` and
`IUpdatePayloadHandler<TPayload>`. Global `IUpdateMiddleware` remains update-wide;
the explicitly named `UseUpdateMiddleware` API provides route compatibility for
existing components. Both façades use the internal generic pipeline kernel.
See D-022 in [DECISIONS.md](DECISIONS.md).

## Resolved - route registration

`UpdateRoute<TPayload>` binds `UpdateType`, payload type and payload extractor.
The internal registry is keyed by `UpdateType`. Built-in descriptors cover known Telegram updates; applications can create strongly typed descriptors for future Telegram.Bot payloads.
`Route(descriptor).HandleWith<THandler>()` enforces payload compatibility through generic constraints.
See D-019 in [DECISIONS.md](DECISIONS.md).

## Closed - multiple handlers versus middleware

Each route owns at most one terminal. Additive extensions use the route-local pipeline; global middleware surrounds general update processing.

## Closed - multi-handler control flow

Terminal chains were eliminated. Priorities, Handled/Continue results and parallel handler fan-out are not needed and are outside scope.
Ordering, short-circuit and exception unwind follow the existing nested middleware contract.
See D-018 in [DECISIONS.md](DECISIONS.md).

## Resolved - defaults and fallback

Explicit routes own the complete pipeline and optional terminal. Built-in message/callback command processing is installed only for routes not explicitly configured.
Routes without terminals and null extracted payloads use update fallback; conversations retain precedence over commands in the default message route.
See D-020 in [DECISIONS.md](DECISIONS.md).
