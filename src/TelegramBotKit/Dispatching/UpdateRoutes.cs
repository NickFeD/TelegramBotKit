using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.Payments;

namespace TelegramBotKit.Dispatching;

/// <summary>Typed descriptors for Telegram.Bot 22.9 update payloads.</summary>
public static class UpdateRoutes
{
    public static UpdateRoute<Message> Message { get; } =
        UpdateRoute.Create(UpdateType.Message, static update => update.Message);

    public static UpdateRoute<Message> EditedMessage { get; } =
        UpdateRoute.Create(UpdateType.EditedMessage, static update => update.EditedMessage);

    public static UpdateRoute<Message> ChannelPost { get; } =
        UpdateRoute.Create(UpdateType.ChannelPost, static update => update.ChannelPost);

    public static UpdateRoute<Message> EditedChannelPost { get; } =
        UpdateRoute.Create(UpdateType.EditedChannelPost, static update => update.EditedChannelPost);

    public static UpdateRoute<BusinessConnection> BusinessConnection { get; } =
        UpdateRoute.Create(UpdateType.BusinessConnection, static update => update.BusinessConnection);

    public static UpdateRoute<Message> BusinessMessage { get; } =
        UpdateRoute.Create(UpdateType.BusinessMessage, static update => update.BusinessMessage);

    public static UpdateRoute<Message> EditedBusinessMessage { get; } =
        UpdateRoute.Create(UpdateType.EditedBusinessMessage, static update => update.EditedBusinessMessage);

    public static UpdateRoute<BusinessMessagesDeleted> DeletedBusinessMessages { get; } =
        UpdateRoute.Create(UpdateType.DeletedBusinessMessages, static update => update.DeletedBusinessMessages);

    public static UpdateRoute<MessageReactionUpdated> MessageReaction { get; } =
        UpdateRoute.Create(UpdateType.MessageReaction, static update => update.MessageReaction);

    public static UpdateRoute<MessageReactionCountUpdated> MessageReactionCount { get; } =
        UpdateRoute.Create(UpdateType.MessageReactionCount, static update => update.MessageReactionCount);

    public static UpdateRoute<InlineQuery> InlineQuery { get; } =
        UpdateRoute.Create(UpdateType.InlineQuery, static update => update.InlineQuery);

    public static UpdateRoute<ChosenInlineResult> ChosenInlineResult { get; } =
        UpdateRoute.Create(UpdateType.ChosenInlineResult, static update => update.ChosenInlineResult);

    public static UpdateRoute<CallbackQuery> CallbackQuery { get; } =
        UpdateRoute.Create(UpdateType.CallbackQuery, static update => update.CallbackQuery);

    public static UpdateRoute<ShippingQuery> ShippingQuery { get; } =
        UpdateRoute.Create(UpdateType.ShippingQuery, static update => update.ShippingQuery);

    public static UpdateRoute<PreCheckoutQuery> PreCheckoutQuery { get; } =
        UpdateRoute.Create(UpdateType.PreCheckoutQuery, static update => update.PreCheckoutQuery);

    public static UpdateRoute<PaidMediaPurchased> PurchasedPaidMedia { get; } =
        UpdateRoute.Create(UpdateType.PurchasedPaidMedia, static update => update.PurchasedPaidMedia);

    public static UpdateRoute<Poll> Poll { get; } =
        UpdateRoute.Create(UpdateType.Poll, static update => update.Poll);

    public static UpdateRoute<PollAnswer> PollAnswer { get; } =
        UpdateRoute.Create(UpdateType.PollAnswer, static update => update.PollAnswer);

    public static UpdateRoute<ChatMemberUpdated> MyChatMember { get; } =
        UpdateRoute.Create(UpdateType.MyChatMember, static update => update.MyChatMember);

    public static UpdateRoute<ChatMemberUpdated> ChatMember { get; } =
        UpdateRoute.Create(UpdateType.ChatMember, static update => update.ChatMember);

    public static UpdateRoute<ChatJoinRequest> ChatJoinRequest { get; } =
        UpdateRoute.Create(UpdateType.ChatJoinRequest, static update => update.ChatJoinRequest);

    public static UpdateRoute<ChatBoostUpdated> ChatBoost { get; } =
        UpdateRoute.Create(UpdateType.ChatBoost, static update => update.ChatBoost);

    public static UpdateRoute<ChatBoostRemoved> RemovedChatBoost { get; } =
        UpdateRoute.Create(UpdateType.RemovedChatBoost, static update => update.RemovedChatBoost);

}
