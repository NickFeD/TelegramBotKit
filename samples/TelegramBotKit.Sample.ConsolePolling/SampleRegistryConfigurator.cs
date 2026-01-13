using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramBotKit.Dispatching;

namespace TelegramBotKit.Sample.ConsolePolling;

/// <summary>
/// Пример расширения: добавляем маппинг EditedMessage -> Message,
/// чтобы тот же MessageUpdateHandler работал и на edit.
/// </summary>
public sealed class SampleRegistryConfigurator : IRegistryConfigurator
{
    public void Configure(UpdateHandlerRegistry reg)
    {
        reg.Map<Message>(UpdateType.EditedMessage, u => u.EditedMessage);
        reg.Map<Message>(UpdateType.ChannelPost, u => u.ChannelPost);
        reg.Map<Message>(UpdateType.EditedChannelPost, u => u.EditedChannelPost);
    }
}
