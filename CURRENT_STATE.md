# TelegramBotKit - Current State

> Updated 2026-09-23.

## CURRENT

- .NET 10 toolkit with core, optional Hosting, Routing and Generators packages.
- Dispatch: per-update scope/context -> global middleware -> UpdateType registry -> typed extraction -> local pipeline -> single terminal/fallback -> unwind -> scope disposal.
- `UpdateRoute<TPayload>` binds identity, payload type and extraction; `UpdateRoutes` provides 23 built-in descriptors.
- Custom descriptors are supported without catalog changes. Shared payload types do not share route ownership.
- `Route(descriptor).Use<TMiddleware>().HandleWith<THandler>()` provides additive middleware and compile-time handler compatibility.
- Duplicate terminals and conflicting descriptors fail early, including across repeated AddTelegramBotKit calls sharing one service collection. Failed DI registrations do not mutate routes. Registry/node implementation stays internal.
- `IUpdatePayloadHandler<TPayload>` is retained. Payload-only `Map` and `AddUpdateHandler` APIs are removed.
- Default Message/CallbackQuery routes retain command behavior; expected conversation responses still take precedence.
- Explicit route registration defines the entire route. Middleware-only routes invoke update fallback after middleware.
- Missing routes and null extracted payloads invoke update fallback in the update scope.
- Middleware is resolved from the per-update scope, with existing explicit lifetimes respected.
- Polling remains the main delivery path; webhook architecture is outside this change. Core owns conversation policy for both direct dispatch and polling; Hosting supplies scheduling only. Replies to active built-in waiters pass global middleware outside the occupied actor/DOP slot.

## Validation

`tests/TelegramBotKit.Tests` covers route isolation, descriptor conflicts, duplicate terminals, generic constraints, custom routes, pipeline ordering/short-circuit/unwind, exceptions, fallbacks, scope disposal, catalog coverage and command/conversation compatibility.
Run `dotnet test TelegramBotKit.slnx -c Release` with .NET 10.
Verified: all 38 tests pass (22 existing + 16 added cases). Core, Hosting, Routing and Generators build in Release. Of the 16 new cases, 11 fail against the saved pre-fix core/Hosting assemblies, confirming the regressions; the other 5 preserve existing behavior. Samples were not changed by this fix.

## Compatibility

See [migration](docs/updates.md). Applications using removed registration APIs must choose explicit routes and migrate extra handlers into middleware.
Global class middleware now defaults to scoped instead of construction from the root provider; pre-registered singleton/transient lifetimes remain respected.
The existing SourceLink build dependency emits NU1902 for Microsoft.Build.Tasks.Git 10.0.103; dependency remediation is outside routing scope.

## PLANNED

Webhook support remains separate. Update-route registration and multi-terminal questions are resolved by D-018 through D-020.
