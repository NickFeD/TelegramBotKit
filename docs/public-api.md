# Public API surface (draft)

This document lists the intentionally public types that make up the supported surface of TelegramBotKit.
Everything else in the repository should be treated as implementation detail and may change.

## TelegramBotKit (core)

### Dependency injection / configuration
- TelegramBotKit.DependencyInjection.TelegramBotKitServiceCollectionExtensions
  - AddTelegramBotKit(...)
  - AddUpdateHandler<TPayload, THandler>(...)
  - AddCommands(...)
  - AddMessageCommand<TCommand>(...)
  - AddTextCommand<TCommand>(...)
  - AddCallbackCommand<TCommand>(...)
- TelegramBotKit.DependencyInjection.TelegramBotKitBuilder

### Pipeline
- TelegramBotKit.BotContext
- TelegramBotKit.Middleware.IUpdateMiddleware

### Fallbacks
- TelegramBotKit.Fallbacks.IDefaultUpdateHandler
- TelegramBotKit.Fallbacks.IDefaultMessageHandler
- TelegramBotKit.Fallbacks.IDefaultCallbackHandler

### Commands
- TelegramBotKit.Commands.ICommand
- TelegramBotKit.Commands.IMessageCommand
- TelegramBotKit.Commands.ITextCommand
- TelegramBotKit.Commands.ICallbackCommand
- TelegramBotKit.Commands.MessageCommandAttribute
- TelegramBotKit.Commands.TextCommandAttribute
- TelegramBotKit.Commands.CallbackCommandAttribute

### Dispatching
- TelegramBotKit.Dispatching.IUpdateDispatcher
- TelegramBotKit.Dispatching.IUpdatePayloadHandler<TPayload>

### Messaging
- TelegramBotKit.Messaging.IMessageSender
- TelegramBotKit.Messaging.SendText / SendPhoto
- TelegramBotKit.Messaging.EditText / EditPhoto
- TelegramBotKit.Messaging.AnswerCallback
- TelegramBotKit.Messaging.QueuedMessageSenderOptions

### Conversations
- TelegramBotKit.Conversations.WaitForUserResponse

### Keyboards
- TelegramBotKit.Keyboards.Keyboard

### Options
- TelegramBotKit.Options.TelegramBotKitOptions
- TelegramBotKit.Options.PollingOptions
- TelegramBotKit.Options.WebhookOptions
- TelegramBotKit.Options.UpdateDeliveryMode

### Exceptions
- TelegramBotKit.Exceptions.TelegramBotKitException and derived exceptions

## TelegramBotKit.Hosting
- TelegramBotKit.Hosting.TelegramBotKitHostingServiceCollectionExtensions

## TelegramBotKit.Routing
- TelegramBotKit.Routing.TelegramBotKitRoutingExtensions

## Notes
- TelegramBotKit.DependencyInjection.TelegramBotKitGeneratedCommandsHook is public only to support the optional source generator.
  It is not intended for direct use.
