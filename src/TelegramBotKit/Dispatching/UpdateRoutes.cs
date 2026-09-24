using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.Payments;

namespace TelegramBotKit.Dispatching;

/// <summary>Typed descriptors for Telegram.Bot 22.9 update payloads.</summary>
public static class UpdateRoutes
{
    /// <summary>
    /// Gets the route for <see cref="UpdateType.Message"/>, extracting <see cref="Update.Message"/> as a <see cref="Message"/> payload.
    /// </summary>
    public static UpdateRoute<Message> Message { get; } =
        UpdateRoute.Create(UpdateType.Message, static update => update.Message);

    /// <summary>
    /// Gets the route for <see cref="UpdateType.EditedMessage"/>, extracting <see cref="Update.EditedMessage"/> as a <see cref="Message"/> payload.
    /// </summary>
    public static UpdateRoute<Message> EditedMessage { get; } =
        UpdateRoute.Create(UpdateType.EditedMessage, static update => update.EditedMessage);

    /// <summary>
    /// Gets the route for <see cref="UpdateType.ChannelPost"/>, extracting <see cref="Update.ChannelPost"/> as a <see cref="Message"/> payload.
    /// </summary>
    public static UpdateRoute<Message> ChannelPost { get; } =
        UpdateRoute.Create(UpdateType.ChannelPost, static update => update.ChannelPost);

    /// <summary>
    /// Gets the route for <see cref="UpdateType.EditedChannelPost"/>, extracting <see cref="Update.EditedChannelPost"/> as a <see cref="Message"/> payload.
    /// </summary>
    public static UpdateRoute<Message> EditedChannelPost { get; } =
        UpdateRoute.Create(UpdateType.EditedChannelPost, static update => update.EditedChannelPost);

    /// <summary>
    /// Gets the route for <see cref="UpdateType.BusinessConnection"/>, extracting <see cref="Update.BusinessConnection"/>.
    /// </summary>
    public static UpdateRoute<BusinessConnection> BusinessConnection { get; } =
        UpdateRoute.Create(UpdateType.BusinessConnection, static update => update.BusinessConnection);

    /// <summary>
    /// Gets the route for <see cref="UpdateType.BusinessMessage"/>, extracting <see cref="Update.BusinessMessage"/> as a <see cref="Message"/> payload.
    /// </summary>
    public static UpdateRoute<Message> BusinessMessage { get; } =
        UpdateRoute.Create(UpdateType.BusinessMessage, static update => update.BusinessMessage);

    /// <summary>
    /// Gets the route for <see cref="UpdateType.EditedBusinessMessage"/>, extracting <see cref="Update.EditedBusinessMessage"/> as a <see cref="Message"/> payload.
    /// </summary>
    public static UpdateRoute<Message> EditedBusinessMessage { get; } =
        UpdateRoute.Create(UpdateType.EditedBusinessMessage, static update => update.EditedBusinessMessage);

    /// <summary>
    /// Gets the route for <see cref="UpdateType.DeletedBusinessMessages"/>, extracting <see cref="Update.DeletedBusinessMessages"/>.
    /// </summary>
    public static UpdateRoute<BusinessMessagesDeleted> DeletedBusinessMessages { get; } =
        UpdateRoute.Create(UpdateType.DeletedBusinessMessages, static update => update.DeletedBusinessMessages);

    /// <summary>
    /// Gets the route for <see cref="UpdateType.MessageReaction"/>, extracting <see cref="Update.MessageReaction"/>.
    /// </summary>
    public static UpdateRoute<MessageReactionUpdated> MessageReaction { get; } =
        UpdateRoute.Create(UpdateType.MessageReaction, static update => update.MessageReaction);

    /// <summary>
    /// Gets the route for <see cref="UpdateType.MessageReactionCount"/>, extracting <see cref="Update.MessageReactionCount"/>.
    /// </summary>
    public static UpdateRoute<MessageReactionCountUpdated> MessageReactionCount { get; } =
        UpdateRoute.Create(UpdateType.MessageReactionCount, static update => update.MessageReactionCount);

    /// <summary>
    /// Gets the route for <see cref="UpdateType.InlineQuery"/>, extracting <see cref="Update.InlineQuery"/>.
    /// </summary>
    public static UpdateRoute<InlineQuery> InlineQuery { get; } =
        UpdateRoute.Create(UpdateType.InlineQuery, static update => update.InlineQuery);

    /// <summary>
    /// Gets the route for <see cref="UpdateType.ChosenInlineResult"/>, extracting <see cref="Update.ChosenInlineResult"/>.
    /// </summary>
    public static UpdateRoute<ChosenInlineResult> ChosenInlineResult { get; } =
        UpdateRoute.Create(UpdateType.ChosenInlineResult, static update => update.ChosenInlineResult);

    /// <summary>
    /// Gets the route for <see cref="UpdateType.CallbackQuery"/>, extracting <see cref="Update.CallbackQuery"/>.
    /// </summary>
    public static UpdateRoute<CallbackQuery> CallbackQuery { get; } =
        UpdateRoute.Create(UpdateType.CallbackQuery, static update => update.CallbackQuery);

    /// <summary>
    /// Gets the route for <see cref="UpdateType.ShippingQuery"/>, extracting <see cref="Update.ShippingQuery"/>.
    /// </summary>
    public static UpdateRoute<ShippingQuery> ShippingQuery { get; } =
        UpdateRoute.Create(UpdateType.ShippingQuery, static update => update.ShippingQuery);

    /// <summary>
    /// Gets the route for <see cref="UpdateType.PreCheckoutQuery"/>, extracting <see cref="Update.PreCheckoutQuery"/>.
    /// </summary>
    public static UpdateRoute<PreCheckoutQuery> PreCheckoutQuery { get; } =
        UpdateRoute.Create(UpdateType.PreCheckoutQuery, static update => update.PreCheckoutQuery);

    /// <summary>
    /// Gets the route for <see cref="UpdateType.PurchasedPaidMedia"/>, extracting <see cref="Update.PurchasedPaidMedia"/>.
    /// </summary>
    public static UpdateRoute<PaidMediaPurchased> PurchasedPaidMedia { get; } =
        UpdateRoute.Create(UpdateType.PurchasedPaidMedia, static update => update.PurchasedPaidMedia);

    /// <summary>
    /// Gets the route for <see cref="UpdateType.Poll"/>, extracting <see cref="Update.Poll"/>.
    /// </summary>
    public static UpdateRoute<Poll> Poll { get; } =
        UpdateRoute.Create(UpdateType.Poll, static update => update.Poll);

    /// <summary>
    /// Gets the route for <see cref="UpdateType.PollAnswer"/>, extracting <see cref="Update.PollAnswer"/>.
    /// </summary>
    public static UpdateRoute<PollAnswer> PollAnswer { get; } =
        UpdateRoute.Create(UpdateType.PollAnswer, static update => update.PollAnswer);

    /// <summary>
    /// Gets the route for <see cref="UpdateType.MyChatMember"/>, extracting <see cref="Update.MyChatMember"/>.
    /// </summary>
    public static UpdateRoute<ChatMemberUpdated> MyChatMember { get; } =
        UpdateRoute.Create(UpdateType.MyChatMember, static update => update.MyChatMember);

    /// <summary>
    /// Gets the route for <see cref="UpdateType.ChatMember"/>, extracting <see cref="Update.ChatMember"/>.
    /// </summary>
    public static UpdateRoute<ChatMemberUpdated> ChatMember { get; } =
        UpdateRoute.Create(UpdateType.ChatMember, static update => update.ChatMember);

    /// <summary>
    /// Gets the route for <see cref="UpdateType.ChatJoinRequest"/>, extracting <see cref="Update.ChatJoinRequest"/>.
    /// </summary>
    public static UpdateRoute<ChatJoinRequest> ChatJoinRequest { get; } =
        UpdateRoute.Create(UpdateType.ChatJoinRequest, static update => update.ChatJoinRequest);

    /// <summary>
    /// Gets the route for <see cref="UpdateType.ChatBoost"/>, extracting <see cref="Update.ChatBoost"/>.
    /// </summary>
    public static UpdateRoute<ChatBoostUpdated> ChatBoost { get; } =
        UpdateRoute.Create(UpdateType.ChatBoost, static update => update.ChatBoost);

    /// <summary>
    /// Gets the route for <see cref="UpdateType.RemovedChatBoost"/>, extracting <see cref="Update.RemovedChatBoost"/>.
    /// </summary>
    public static UpdateRoute<ChatBoostRemoved> RemovedChatBoost { get; } =
        UpdateRoute.Create(UpdateType.RemovedChatBoost, static update => update.RemovedChatBoost);

}
