namespace TelegramBotKit.Dispatching;

/// <summary>
/// Carries the non-null typed payload and bot context through one route-local pipeline execution.
/// </summary>
/// <typeparam name="TPayload">The payload selected by the update route.</typeparam>
public sealed class UpdateRouteContext<TPayload>
    where TPayload : class
{
    internal UpdateRouteContext(BotContext botContext, TPayload payload)
    {
        ArgumentNullException.ThrowIfNull(botContext);
        ArgumentNullException.ThrowIfNull(payload);
        BotContext = botContext;
        Payload = payload;
    }

    /// <summary>Gets the update-wide context for the current route execution.</summary>
    public BotContext BotContext { get; }

    /// <summary>Gets the non-null payload extracted by the selected route.</summary>
    public TPayload Payload { get; }

    internal UpdateRouteContext<TPayload> WithBotContext(BotContext context) =>
        ReferenceEquals(context, BotContext) ? this : new(context, Payload);
}
