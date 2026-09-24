namespace TelegramBotKit.Dispatching;

/// <summary>Represents the next stage of a typed route-local pipeline.</summary>
/// <typeparam name="TPayload">The payload selected by the update route.</typeparam>
/// <param name="context">The typed context to pass to the next route stage.</param>
/// <returns>A task that represents downstream route execution.</returns>
public delegate Task UpdateRouteDelegate<TPayload>(UpdateRouteContext<TPayload> context)
    where TPayload : class;
