namespace TelegramBotKit.Middleware;

/// <summary>
/// Middleware для обработки одного апдейта.
/// </summary>
public delegate Task UpdateMiddleware(BotContext ctx, Func<Task> next);

/// <summary>
/// Простой middleware pipeline, но для BotContext.
/// </summary>
public sealed class MiddlewarePipeline
{
    private readonly List<UpdateMiddleware> _middlewares = new();
    private volatile bool _isFrozen;

    /// <summary>
    /// Добавить middleware в конец цепочки.
    /// Вызывать на этапе конфигурации.
    /// </summary>
    public MiddlewarePipeline Use(UpdateMiddleware middleware)
    {
        if (middleware is null) throw new ArgumentNullException(nameof(middleware));
        EnsureNotFrozen();

        _middlewares.Add(middleware);
        return this;
    }

    /// <summary>
    /// Добавить middleware, которое будет вызываться только если условие true.
    /// </summary>
    public MiddlewarePipeline UseWhen(Func<BotContext, bool> predicate, UpdateMiddleware middleware)
    {
        if (predicate is null) throw new ArgumentNullException(nameof(predicate));
        if (middleware is null) throw new ArgumentNullException(nameof(middleware));
        EnsureNotFrozen();

        _middlewares.Add(async (ctx, next) =>
        {
            if (predicate(ctx))
                await middleware(ctx, next).ConfigureAwait(false);
            else
                await next().ConfigureAwait(false);
        });

        return this;
    }

    /// <summary>
    /// "Заморозить" pipeline: после этого Use/UseWhen нельзя вызывать.
    /// Это защищает от гонок и неожиданных изменений во время работы.
    /// </summary>
    public void Freeze() => _isFrozen = true;

    /// <summary>
    /// Выполнить pipeline и затем финальный обработчик.
    /// </summary>
    public Task ExecuteAsync(BotContext ctx, Func<Task> terminal)
    {
        if (ctx is null) throw new ArgumentNullException(nameof(ctx));
        if (terminal is null) throw new ArgumentNullException(nameof(terminal));

        // Собираем цепочку один раз при первом запуске после Freeze(),
        // но даже без кэша это ок (кол-во middleware обычно небольшое).
        // Если захочешь — позже добавим кэш delegate.
        Func<Task> next = terminal;

        for (int i = _middlewares.Count - 1; i >= 0; i--)
        {
            var current = _middlewares[i];
            var capturedNext = next;
            next = () => current(ctx, capturedNext);
        }

        return next();
    }

    private void EnsureNotFrozen()
    {
        if (_isFrozen)
            throw new InvalidOperationException("MiddlewarePipeline is frozen. Configure it before bot starts.");
    }
}
