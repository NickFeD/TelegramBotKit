namespace TelegramBotKit.Middleware;

public interface IUpdateMiddleware
{
    Task InvokeAsync(BotContext ctx, BotContextDelegate next);
}
