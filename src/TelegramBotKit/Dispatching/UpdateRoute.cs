using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace TelegramBotKit.Dispatching;

/// <summary>Binds an update type to its typed payload selector.</summary>
public sealed class UpdateRoute<TPayload> where TPayload : class
{
    internal UpdateRoute(UpdateType updateType, Func<Update, TPayload?> payloadSelector)
        => (UpdateType, PayloadSelector) = (updateType, payloadSelector);
    public UpdateType UpdateType { get; }
    internal Func<Update, TPayload?> PayloadSelector { get; }
}

/// <summary>Creates typed routes, including routes for future Telegram updates.</summary>
public static class UpdateRoute
{
    public static UpdateRoute<TPayload> Create<TPayload>(UpdateType updateType,
        Func<Update, TPayload?> payloadSelector) where TPayload : class
    {
        ArgumentNullException.ThrowIfNull(payloadSelector);
        return new(updateType, payloadSelector);
    }
}
