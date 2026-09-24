# Messaging

[Docs index](README.md) · [Keyboards](keyboards.md)

`IMessageSender` is the application-facing façade for sending and editing Telegram
messages and answering callbacks. Access the per-update instance through
`BotContext.Sender`.

```csharp
await context.Sender.SendText(
    message.Chat.Id,
    new SendText { Text = "Hello." },
    context.CancellationToken);
```

Request models include `SendText`, `SendPhoto`, `EditText`, `EditPhoto`, and
`AnswerCallback`. Extension methods accept `Message` or `CallbackQuery` when those
objects already contain the required identifiers.

## Forum topics

`SendText` and `SendPhoto` expose `int? MessageThreadId`. These are all the
new-message request models currently provided by TelegramBotKit; both map to
thread-aware methods in the installed `Telegram.Bot` **22.9.0** package.
Edits and callback answers do not take a thread ID.

Sending from a `Message` preserves its chat and thread:

```csharp
await context.Sender.SendText(message, new SendText
{
    Text = "Hello from the same topic"
}, context.CancellationToken);
```

For an explicit destination, specify the thread on the request:

```csharp
await context.Sender.SendText(chatId, new SendText
{
    Text = "Hello",
    MessageThreadId = topicId
}, context.CancellationToken);
```

The same rules apply to photos, callbacks with a `Message`, and the corresponding
`Try*` methods. Precedence is **request thread > source message thread > null**.
Convenience sends copy the immutable request; they do not change the caller's
request. A null request thread means "inherit" for message-based operations.
To send without inheriting a source thread, use the explicit `chatId` overload
with a null `MessageThreadId`.

`ReplyText` and `ReplyPhoto` also inherit the source thread unless overridden.
They send both `message_thread_id` and reply parameters identifying the original
message; changing the thread does not change the reply target. Telegram determines
whether that reply target is valid in the destination.

Direct and queued senders use the same rules. The queue retains the request and
delegates to the direct sender, including on retries. Rate limits remain per chat,
not per topic. Ordinary messages with no thread continue to omit the thread field.

Use the thread ID supplied by Telegram (from `Message.MessageThreadId` or the
`ForumTopic.MessageThreadId` returned by creation). It identifies a topic within
its chat, not a globally unique destination. The Bot API supports this send
parameter for forum supergroups and private chats of bots with forum topic mode
enabled; it is distinct from `direct_messages_topic_id`.
See the [Telegram sendMessage specification](https://core.telegram.org/bots/api#sendmessage).

### Topic management and service messages

Use `BotContext.BotClient` with `using Telegram.Bot;` for topic administration.
These operations manage chat state rather than send messages, so they remain
outside `IMessageSender` and its optional outgoing queue. The installed 22.9.0
client provides the following methods (all accept `cancellationToken`):

| Methods | Destination and other parameters |
| --- | --- |
| `CreateForumTopic` | `chatId`, `name`, optional `iconColor`, `iconCustomEmojiId`; returns `ForumTopic` |
| `EditForumTopic` | `chatId`, `messageThreadId`, optional `name`, `iconCustomEmojiId` |
| `CloseForumTopic`, `ReopenForumTopic`, `DeleteForumTopic` | `chatId`, `messageThreadId` |
| `EditGeneralForumTopic` | `chatId`, `name` |
| `CloseGeneralForumTopic`, `ReopenGeneralForumTopic` | `chatId` |
| `HideGeneralForumTopic`, `UnhideGeneralForumTopic` | `chatId` |
| `UnpinAllForumTopicMessages` | `chatId`, `messageThreadId` |
| `UnpinAllGeneralForumTopicMessages` | `chatId` |
| `GetForumTopicIconStickers` | No destination; returns available topic icon stickers |

```csharp
var topic = await context.BotClient.CreateForumTopic(
    chatId: message.Chat.Id,
    name: "Support",
    cancellationToken: context.CancellationToken);

await context.Sender.SendText(message.Chat.Id, new SendText
{
    Text = "Welcome to Support",
    MessageThreadId = topic.MessageThreadId
}, context.CancellationToken);

await context.BotClient.EditForumTopic(
    chatId: message.Chat.Id,
    messageThreadId: topic.MessageThreadId,
    name: "Customer support",
    cancellationToken: context.CancellationToken);
```

General is a special forum topic: its management methods take only the chat
destination, rather than a thread ID. Hiding General also closes it; reopening
also unhides it. For a topic-less send use the explicit chat overload with no
thread, rather than inventing a thread ID. TelegramBotKit does not normalize or
replace IDs received from Telegram.

In supergroups, creation requires the administrator right `CanManageTopics`;
editing/closing/reopening also require it unless the bot created the topic.
Deletion requires `CanDeleteMessages` and deletes the topic's messages too.
General management requires `CanManageTopics`; clearing pins requires
`CanPinMessages`. Telegram enforces permissions and topic availability.
See [forum topic administration](https://core.telegram.org/bots/api#createforumtopic).

Incoming service events are already exposed on Telegram's `Message` as
`ForumTopicCreated`, `ForumTopicEdited`, `ForumTopicClosed`, `ForumTopicReopened`,
`GeneralForumTopicHidden`, and `GeneralForumTopicUnhidden`, with corresponding
`MessageType` values. `Message.IsTopicMessage` identifies topic messages and
`Message.MessageThreadId` is nullable. Read these fields directly from your
payload; `BotContext` does not infer a topic from arbitrary update types or keep
mutable topic state.

## Callback queries and inline mode

A callback created from a chat message has `CallbackQuery.Message`; an inline callback
does not. Methods such as `SendText(callback, ...)` that require chat and message IDs
throw when that message is unavailable.

Use the corresponding `Try*` method when either callback form is valid:

```csharp
if (context.Sender.TryEditText(
    callback,
    new EditText { Text = "Updated" },
    out var edit,
    context.CancellationToken) && edit is not null)
{
    await edit;
}
```

`TrySendText`, `TryReplyText`, `TrySendPhoto`, `TryReplyPhoto`, `TryEditText`, `TryEditReplyMarkup`, and
`TryEditPhoto` return false and set their task to null when
`CallbackQuery.Message` is unavailable.

## Optional queued sender

The default sender performs operations directly. Enable the queued implementation
when the application needs centralized rate limiting and retry behavior:

```csharp
bot.UseQueuedMessageSender(options =>
{
    options.GlobalMaxPerSecond = 25;
    options.PerChatMinDelay = TimeSpan.FromSeconds(1);
    options.MaxRetryAttempts = 5;
});
```

Queued sending is optional and is not required by the minimal bot setup.
