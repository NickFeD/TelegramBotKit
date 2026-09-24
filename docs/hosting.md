# Hosting and polling

← [Docs index](README.md) · See also: [Processing pipeline](processing-pipeline.md)

`TelegramBotKit.Hosting` provides polling integration (`GetUpdates`) and an update scheduler.

## Enable polling

```csharp
builder.Services.AddTelegramBotKitPolling();
```

Polling options are available on `TelegramBotKitOptions.Polling`:

- `MaxDegreeOfParallelism` — global concurrency.
- `Limit` — `GetUpdates` batch size.
- `TimeoutSeconds` — `GetUpdates` long polling timeout.
- `AllowedUpdates` — Telegram update types to request.

## Update scheduling

By default the scheduler processes updates on "actors" to reduce race conditions:

- Message-like updates are keyed by **Chat**.
- Inline-query-like updates are keyed by **User**.
- Callback queries are keyed by **User** (to avoid concurrent callbacks for the same user).

This lets request/response flows (see `WaitForUserResponse`) work reliably while still enabling parallelism.

Additional notes:

- Some update types are **unkeyed** (for example, `UpdateType.Poll`). These are processed immediately and only respect the global DOP limit.
- Idle actors are cleaned up after a short period of inactivity.

## WaitForUserResponse gotchas

`WaitForUserResponse` is a small helper for “ask a question → wait for the next message from this user”.

- For the built-in Message route, core tries to publish incoming messages to an active waiter after global middleware and before command routing. Explicit Message routes retain full ownership and do not automatically publish to waiters.
- Only **one active wait** is allowed per `(chatId, userId)`. If you call `WaitAsync` again while a previous wait is still active, it throws:
  `InvalidOperationException: Already waiting for message from chat:... user:...`

See `./conversations.md` for recommended usage patterns.

## Global rate limiting

Polling uses a global DOP limiter (`MaxDegreeOfParallelism`).

- Set it to a lower value for stricter global concurrency.
- Set it to `0` to **remove the global limit** (unlimited concurrency; actors still serialize per key).

## Conversation response scheduling

Polling forwards every update to the scheduler. Routing/conversation policy stays in the core dispatcher.
For the built-in Message route, a potential response to an already active waiter runs through global middleware in a separate update scope outside the actor and global DOP slot held by the waiting command. Middleware can still short-circuit delivery.
If the waiter disappears while middleware is running, the remaining routing continuation returns to the scheduler without rerunning middleware. The response scope is disposed only after its pipeline unwinds.
This exception to the concurrency limit is limited to response processing; normal commands and explicit routes keep actor/DOP scheduling.
