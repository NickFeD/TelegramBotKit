using TelegramBotKit.Dispatching;

namespace TelegramBotKit.DependencyInjection;

internal sealed class DelegateRegistryConfigurator : IRegistryConfigurator
{
    private readonly Action<UpdateHandlerRegistry> _action;
    public DelegateRegistryConfigurator(Action<UpdateHandlerRegistry> action) => _action = action;
    public void Configure(UpdateHandlerRegistry registry) => _action(registry);
}
