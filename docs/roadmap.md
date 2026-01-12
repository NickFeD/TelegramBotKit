# TelegramBotKit Roadmap

## Сейчас (MVP)
- Polling (GetUpdates)
- BotContext + MiddlewarePipeline
- Команды через [Command] + DI autoscan (по сборкам)
- IMessageCommand / ITextCommand / ICallbackCommand
- WaitForUserResponse (in-memory)

## Ближайшее
- README с QuickStart + примеры
- TelegramBotKitException + базовые наследники (Configuration/Dispatch/CallbackData)
- Улучшения MessageSender (тонкий wrapper и удобные методы)

## Среднесрочно
- Actor per user/chat dispatch strategy (без глобального залипания при ожиданиях)
- Мини-фреймворк в стиле ASP.NET Minimal API (TelegramBotKit.Routing)
- Webhook hosting (опционально)
- AOT: Source Generator для регистрации команд вместо reflection

## Позже
- TelegramBotKit.Dialogs (state-machine сценарии)
- Storage abstractions + Redis implementation
