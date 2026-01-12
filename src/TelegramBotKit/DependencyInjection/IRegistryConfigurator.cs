using TelegramBotKit.Dispatching;

namespace TelegramBotKit.DependencyInjection;

internal interface IRegistryConfigurator
{
    void Configure(UpdateHandlerRegistry registry);
}
