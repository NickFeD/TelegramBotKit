namespace TelegramBotKit.Middleware;

/// <summary>
/// Middleware как класс (аналог ASP.NET).
/// ВАЖНО: middleware создаётся один раз и используется многопоточно => должен быть потокобезопасным.
/// Для "состояния на апдейт" используй ctx.Items.
/// </summary>
public interface IUpdateMiddleware
{
    Task InvokeAsync(BotContext ctx, BotContextDelegate next);
}
