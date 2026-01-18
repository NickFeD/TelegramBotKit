# Hosting and polling

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

## Global rate limiting

Polling uses a global DOP limiter. If you need stricter control, set `MaxDegreeOfParallelism` to a lower value (or `0` to disable).
