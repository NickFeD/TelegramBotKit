using Telegram.Bot.Types;

namespace TelegramBotKit.Dispatching;

/// <summary>
/// Обработчик конкретного payload из Update (Message, CallbackQuery, InlineQuery, ...).
/// </summary>
public interface IUpdatePayloadHandler<TPayload> where TPayload : class
{
    Task HandleAsync(TPayload payload, BotContext ctx, CancellationToken ct);
}

// Ниже маркерные интерфейсы для удобства.

public interface IInlineQueryHandler : IUpdatePayloadHandler<InlineQuery> { }
public interface IChosenInlineResultHandler : IUpdatePayloadHandler<ChosenInlineResult> { }
public interface IMyChatMemberHandler : IUpdatePayloadHandler<ChatMemberUpdated> { }
public interface IChatMemberHandler : IUpdatePayloadHandler<ChatMemberUpdated> { }
public interface IPollHandler : IUpdatePayloadHandler<Poll> { }
public interface IPollAnswerHandler : IUpdatePayloadHandler<PollAnswer> { }
public interface IChatJoinRequestHandler : IUpdatePayloadHandler<ChatJoinRequest> { }

// и т.д. — можно расширять по мере надобности.
// При этом “ядро” всё равно generic: IUpdatePayloadHandler<TPayload>.
