namespace TelegramBotKit.Dispatching;

internal sealed class UpdateRouteContext<TPayload>(BotContext botContext, TPayload payload)
    where TPayload : class
{
    internal BotContext BotContext { get; } = botContext;
    internal TPayload Payload { get; } = payload;

    internal UpdateRouteContext<TPayload> WithBotContext(BotContext context) =>
        ReferenceEquals(context, BotContext) ? this : new(context, Payload);
}
