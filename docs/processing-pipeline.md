# Processing pipeline

[Docs index](README.md) | [Updates and migration](updates.md)

## Runtime order

```text
Telegram Update
  -> optional hosting / scheduling
  -> per-update DI scope and BotContext
  -> global middleware
  -> registry lookup by Update.Type
  -> route descriptor extracts TPayload
  -> route-local middleware
  -> 0..1 terminal handler or update fallback
  -> local middleware unwind
  -> global middleware unwind
  -> asynchronous scope disposal
```

The route owns its payload selector, middleware and optional terminal. The registry never searches handlers by payload type or enumerates DI handler services.
Different update types carrying the same payload have independent routes.
Concrete handlers and middleware resolve from the update scope with their configured DI lifetimes.

## Middleware

Global middleware surrounds routing and fallback. Local middleware belongs only to its selected route and runs after successful extraction.
First registered is outermost; `await next(ctx)` awaits downstream work and unwinds. Not invoking next short-circuits; exceptions propagate through both pipelines. Scope disposal occurs even when processing throws.
There is no separate handler chain or Handled/Continue result.

## Default message route

If Message has not been explicitly configured, its built-in flow executes:

1. Core dispatcher calls `WaitForUserResponse.TryPublish(message)` after global middleware, gated by ownership of the built-in Message terminal; an expected response consumes the update.
2. Slash command routing, then exact text triggers.
3. `IDefaultMessageHandler` if no command matches.

Conversation precedence remains unchanged. EditedMessage does not use this route merely because it carries Message.

## Default callback route

If CallbackQuery has not been explicitly configured, its built-in terminal tries callback commands, then `IDefaultCallbackHandler`.
Callback data retains the format `{key} {arg1} {arg2} ...`.

Explicit `Route(...)` registration owns the entire route and opts out of its default terminal. Use global middleware to wrap the default command/conversation flow.

## Fallbacks

- `IDefaultUpdateHandler`: unregistered update type, route without terminal, or null selected payload.
- `IDefaultMessageHandler`: default message route, after conversations and unmatched commands.
- `IDefaultCallbackHandler`: default callback route, after unmatched commands.

A middleware-only route invokes update fallback at its end. Null payloads skip local middleware and invoke update fallback inside the global pipeline.
Default fallback implementations are no-ops. Fallback service resolution uses the update scope.

See [middleware](middleware.md), [commands](commands-and-routing.md), and [conversations](conversations.md).
