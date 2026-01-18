# Middleware

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

- Middleware is typically singleton.
- If you need per-update scoped services, resolve them via `ctx.Services`.

## Ordering

Middlewares run in the order you register them:

- First registered middleware is executed first (outermost).
- Last registered middleware is executed last (innermost).
