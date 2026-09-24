using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace TelegramBotKit.Dispatching;

/// <summary>
/// Identifies one Telegram update type and permanently binds it to a typed payload selector.
/// </summary>
/// <typeparam name="TPayload">The non-null payload type exposed to route middleware and its terminal.</typeparam>
public sealed class UpdateRoute<TPayload> where TPayload : class
{
    internal UpdateRoute(UpdateType updateType, Func<Update, TPayload?> payloadSelector)
        => (UpdateType, PayloadSelector) = (updateType, payloadSelector);
    /// <summary>Gets the Telegram update identity used to select this route at runtime.</summary>
    public UpdateType UpdateType { get; }
    internal Func<Update, TPayload?> PayloadSelector { get; }
}

/// <summary>
/// Creates typed route descriptors for custom or future Telegram update types.
/// </summary>
public static class UpdateRoute
{
    /// <summary>
    /// Creates a strongly typed route descriptor that binds one Telegram update type to the
    /// selector used to extract its payload. Applications can use this for custom or future
    /// Telegram.Bot update types that are not listed by <see cref="UpdateRoutes"/>.
    /// </summary>
    /// <typeparam name="TPayload">The non-null payload type processed by the route.</typeparam>
    /// <param name="updateType">The Telegram update identity used for route lookup.</param>
    /// <param name="payloadSelector">A selector that extracts the payload or returns null when it is unavailable.</param>
    /// <returns>A descriptor that preserves the update identity, payload type, and selector.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="payloadSelector"/> is null.</exception>
    public static UpdateRoute<TPayload> Create<TPayload>(UpdateType updateType,
        Func<Update, TPayload?> payloadSelector) where TPayload : class
    {
        ArgumentNullException.ThrowIfNull(payloadSelector);
        return new(updateType, payloadSelector);
    }
}
