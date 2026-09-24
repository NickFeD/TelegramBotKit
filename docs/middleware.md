# Middleware

[Docs index](README.md) · [Processing pipeline](processing-pipeline.md) · [Typed routes](updates.md)

TelegramBotKit has two middleware façades and one terminal contract:

| Role | Contract | Context | Scope |
|---|---|---|---|
| Global middleware | `IUpdateMiddleware` | `BotContext` | Every update type |
| Typed route middleware | `IUpdateRouteMiddleware<TPayload>` | `UpdateRouteContext<TPayload>` | One configured route |
| Route terminal | `IUpdatePayloadHandler<TPayload>` | Payload plus `BotContext` | Final owner of one route |

Both middleware contracts use nested execution: the first registered component is
outermost, omitting `next` short-circuits downstream processing, and exceptions unwind
through upstream middleware.

## Global middleware

Use global middleware for logging, metrics, authorization, throttling, and other
update-wide policies:

```csharp
bot.UseMiddleware(async (context, next) =>
{
    // Before routing.
    await next(context);
    // After routing and its terminal/fallback.
});
```

For paths that often complete synchronously, a `ValueTask` overload is available:

```csharp
bot.UseMiddleware((context, next) =>
{
    if (context.Update.Id % 2 == 0)
        return new ValueTask(next(context));

    return ValueTask.CompletedTask;
});
```

Class-based global middleware implements `IUpdateMiddleware`:

```csharp
using TelegramBotKit.Middleware;

public sealed class TraceMiddleware(ILogger<TraceMiddleware> log) : IUpdateMiddleware
{
    public async Task InvokeAsync(BotContext context, BotContextDelegate next)
    {
        log.LogInformation("Handling {UpdateType}", context.Update.Type);
        await next(context);
    }
}

bot.UseMiddleware<TraceMiddleware>();
```

## Typed route middleware

Use typed route middleware when behavior depends on one route's payload:

```csharp
using Telegram.Bot.Types;
using TelegramBotKit.Dispatching;

public sealed class EditedMessageFilter : IUpdateRouteMiddleware<Message>
{
    public Task InvokeAsync(
        UpdateRouteContext<Message> context,
        UpdateRouteDelegate<Message> next)
    {
        if (context.Payload.Text is null)
            return Task.CompletedTask;

        return next(context);
    }
}

bot.Route(UpdateRoutes.EditedMessage)
   .Use<EditedMessageFilter>()
   .HandleWith<EditedMessageHandler>();
```

The payload has already been extracted and is available as `context.Payload`.
See [typed update routes](updates.md) for inline middleware, route ownership, and
custom descriptors.

`UseUpdateMiddleware(...)` exists only to adapt an existing `IUpdateMiddleware` to a
route. Prefer `.Use(...)` and `IUpdateRouteMiddleware<TPayload>` for new route-local
code.

## Lifetimes

Class middleware defaults to scoped. Existing concrete DI registrations are
respected, and scoped dependencies remain alive until both pipelines unwind. An
explicit singleton must not capture scoped services.
