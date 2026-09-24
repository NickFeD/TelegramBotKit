# Middleware

← [Docs index](README.md) · See also: [Processing pipeline](processing-pipeline.md)

TelegramBotKit processes every update through a middleware pipeline (similar to ASP.NET Core).

## Inline middleware

```csharp
bot.UseMiddleware(async (ctx, next) =>
{
    // before
    await next(ctx);
    // after
});
```

For minimal allocations, you can also use the `ValueTask` overload:

```csharp
bot.UseMiddleware((ctx, next) =>
{
    if (ctx.Update.Id % 2 == 0)
        return new ValueTask(next(ctx));

    return ValueTask.CompletedTask; // stop the pipeline
});
```

## Class-based middleware

Implement `IUpdateMiddleware`:

```csharp
using TelegramBotKit.Middleware;

public sealed class TraceMiddleware : IUpdateMiddleware
{
    private readonly ILogger<TraceMiddleware> _log;

    public TraceMiddleware(ILogger<TraceMiddleware> log) => _log = log;

    public async Task InvokeAsync(BotContext ctx, BotContextDelegate next)
    {
        _log.LogInformation("<< {UpdateId}", ctx.Update.Id);
        await next(ctx);
        _log.LogInformation(">> {UpdateId}", ctx.Update.Id);
    }
}
```

Register and use:

```csharp
builder.Services.AddSingleton<TraceMiddleware>();
bot.UseMiddleware<TraceMiddleware>();
```

### Lifetimes

- Class middleware defaults to scoped; existing concrete DI registrations are respected.
- Global and route middleware resolve from the per-update scope, supporting scoped constructor dependencies.
- Explicit singletons must not capture scoped dependencies.
- `bot.Route(descriptor).Use<TMiddleware>()` adds middleware to one route; see [update routes](updates.md) for default-route ownership and migration.

## Ordering

Middlewares run in the order you register them:

- First registered middleware is executed first (outermost).
- Last registered middleware is executed last (innermost).

If middleware does **not** call `next(ctx)`, the pipeline stops and routing/fallback handlers will not run.
