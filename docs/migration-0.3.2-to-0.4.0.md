# Переход с 0.3.2 на 0.4.0

[Документация](README.md) · [Команды](commands-and-routing.md) · [Маршруты](updates.md)

Инструкция подготовлена для выпуска **0.4.0** по изменениям относительно тега
`v0.3.2`. В репозитории подготовлена версия `0.4.0`; команды установки
ниже предназначены для выполнения после публикации стабильных пакетов 0.4.0.

Обновление содержит несовместимые изменения API. Потребуется пересобрать приложение
и библиотеки, использующие TelegramBotKit. Требование **.NET 10** сохраняется.

## 1. Обновите зависимости

Для polling-бота минимальный набор ссылок теперь выглядит так:

```xml
<ItemGroup>
  <PackageReference Include="TelegramBotKit" Version="0.4.0" />
  <PackageReference Include="TelegramBotKit.Polling" Version="0.4.0" />
</ItemGroup>
```

| Пакет в 0.3.2 | Действие для 0.4.0 |
| --- | --- |
| `TelegramBotKit` | Обновите до `0.4.0`; генератор команд уже внутри. |
| `TelegramBotKit.Hosting` | Для polling замените прямую ссылку на `TelegramBotKit.Polling`. Hosting подтянется транзитивно. Сохраните прямую ссылку версии `0.4.0`, если явно используете общий механизм обработки. |
| `TelegramBotKit.Routing` | Обновите до `0.4.0`, если используете `UseMessageCommand`, `UseTextCommand`, `UseCallbackCommand`. |
| `TelegramBotKit.Generators` | Удалите отдельную ссылку: самостоятельный пакет больше не выпускается. |
| `TelegramBotKit.Webhook.AspNetCore` | Новый необязательный пакет для ASP.NET Core webhook. Для polling не нужен. |

Например, в каталоге проекта polling-бота:

```sh
dotnet remove package TelegramBotKit.Hosting
dotnet remove package TelegramBotKit.Generators
dotnet add package TelegramBotKit --version 0.4.0
dotnet add package TelegramBotKit.Polling --version 0.4.0
```

Удаляйте только существующие ссылки. Если используете Routing, отдельно обновите
его до `0.4.0`. При Central Package Management меняйте версии в
`Directory.Packages.props`. Обновите используемые пакеты TelegramBotKit согласованно
во всех проектах решения; после изменения зависимостей обновите и проверьте
`packages.lock.json`, если он используется.

## 2. Перенесите регистрацию polling

В 0.3.2 метод находился в Hosting:

```csharp
using TelegramBotKit.Hosting;

builder.Services.AddTelegramBotKitPolling();
```

В 0.4.0 замените пространство имён:

```csharp
using TelegramBotKit.Polling;

builder.Services.AddTelegramBotKitPolling();
```

Название метода не изменилось. Он сам подключает общий механизм обработки из
Hosting; дополнительный `AddTelegramBotKitUpdateProcessing()` для обычного polling
не требуется. Отдельный `using TelegramBotKit.Hosting` нужен, если используете
`UpdateProcessingOptions` или другие API общего механизма обработки.

Существующая секция `TelegramBotKit:Polling` сохраняется. Значения `Limit`,
`TimeoutSeconds`, `AllowedUpdates` и `MaxDegreeOfParallelism` не нужно переносить
ради обновления. Для общей настройки параллелизма обоих транспортов можно задать:

```csharp
using Microsoft.Extensions.DependencyInjection;
using TelegramBotKit.Hosting;

builder.Services.Configure<UpdateProcessingOptions>(options =>
    options.MaxDegreeOfParallelism = 8);
```

Эта настройка имеет приоритет над `TelegramBotKitOptions.Polling.MaxDegreeOfParallelism`.
При `null` используется прежнее значение из Polling; `0` снимает общий лимит.
Фильтр `AllowedUpdates` управляет подпиской Telegram и сам по себе не добавляет
обработчики в приложение.

## 3. Замените Map и AddUpdateHandler

Удалены `TelegramBotKitBuilder.Map<TPayload>()` и обе формы
`AddUpdateHandler<TPayload, THandler>()` — на builder и на `IServiceCollection`.
Вместо регистрации обработчиков только по типу payload теперь настраивается
маршрут конкретного Telegram `UpdateType`.

Было в 0.3.2:

```csharp
bot.Map<Message>(UpdateType.EditedMessage, static update => update.EditedMessage);
bot.AddUpdateHandler<Message, EditedMessageHandler>();
```

Стало в 0.4.0:

```csharp
using TelegramBotKit.DependencyInjection;
using TelegramBotKit.Dispatching;

bot.Route(UpdateRoutes.EditedMessage)
   .HandleWith<EditedMessageHandler>();
```

Сигнатура `IUpdatePayloadHandler<TPayload>.HandleAsync(TPayload, BotContext)`
сохранена: существующий класс обработчика можно использовать как terminal.
Прямую DI-регистрацию `IUpdatePayloadHandler<TPayload>` тоже замените на
`Route(...).HandleWith<THandler>()`; маршрутизатор больше не перебирает эти сервисы.
`HandleWith` регистрирует конкретный класс в DI, по умолчанию со scoped lifetime.

Учтите изменения поведения:

- `Message` и `EditedMessage` независимы, хотя оба содержат `Message`.
  Если один класс должен обрабатывать оба вида, настройте каждый маршрут явно.
- У маршрута может быть только один terminal. Повторный `HandleWith` приводит
  к ошибке конфигурации. Прежнюю цепочку нескольких payload-обработчиков перенесите
  в middleware через `.Use<TMiddleware>()` и один завершающий `.HandleWith<THandler>()`
  либо вызывайте нужные операции из одного terminal.
- Для собственного selector создайте дескриптор через `UpdateRoute.Create(...)`
  и повторно используйте тот же экземпляр. Нельзя заменять уже настроенный
  `UpdateType` другим дескриптором.
- Настройте маршруты и middleware до первого разрешения dispatcher из DI:
  после этого конфигурация замораживается.
- У незарегистрированного маршрута, маршрута без terminal или при `null` payload
  вызывается `IDefaultUpdateHandler`. При `null` локальные middleware не запускаются.

Пример собственного дескриптора:

```csharp
using Telegram.Bot.Types.Enums;
using TelegramBotKit.Dispatching;

var edited = UpdateRoute.Create(
    UpdateType.EditedMessage,
    static update => update.EditedMessage);

bot.Route(edited).HandleWith<EditedMessageHandler>();
```

### Сохраните встроенную обработку команд

Если бот использует только команды, **не добавляйте**
`Route(UpdateRoutes.Message)` и `Route(UpdateRoutes.CallbackQuery)` при миграции.
Они подключаются автоматически и обрабатывают команды и соответствующие fallback.
Встроенный Message-маршрут также передаёт сообщения в `WaitForUserResponse`.

Явный вызов `Route` для одного из этих типов передаёт вам весь маршрут и отключает
его встроенный terminal, даже если вы добавили только middleware. Это может
остановить команды и доставку ответов ожидающему диалогу. Чтобы обернуть существующую
обработку логированием или проверкой доступа, используйте глобальный
`bot.UseMiddleware<TMiddleware>()`.

Подробнее: [типизированные маршруты](updates.md) и [middleware](middleware.md).

## 4. Проверьте генерацию команд и callback-кнопки

`services.AddCommands()` сохраняется, но теперь использует только регистрации,
созданные встроенным генератором при компиляции. Поиск атрибутов в загруженных
сборках через reflection удалён. Не исключайте analyzer assets пакета Core.

Параметр `fallbackToReflection` сохранён для совместимости сигнатуры:

- `AddCommands()` при отсутствии generated registrar оставляет приложение без
  автоматически зарегистрированных команд;
- `AddCommands(fallbackToReflection: false)` в этом случае выбрасывает
  `InvalidOperationException` и помогает обнаружить отсутствие генерации при запуске;
- значение `true` больше не включает reflection.

Если команды приходят из динамически загружаемых сборок, зарегистрируйте их явно
через `AddMessageCommand<T>`, `AddTextCommand<T>`, `AddCallbackCommand<T>` либо
API пакета Routing. Не рассчитывайте на автоматическое объединение registrar
нескольких сборок: у generated hooks сохраняется ограничение «последняя запись
заменяет предыдущую».

`Keyboard.Callback<THandler>()` тоже требует generated mapping. При его отсутствии
будет `InvalidOperationException`. В случае явной регистрации используйте явный ключ:

```csharp
builder.Services.AddCallbackCommand<LikeCommand>("like");
var button = Keyboard.Callback("Нравится", "like", "42");
```

Здесь `LikeCommand` — ваш класс `ICallbackCommand`; `Keyboard` находится в
`TelegramBotKit.Keyboards`. Ключ кнопки должен совпадать с ключом регистрации.

При использовании исходников через `ProjectReference` analyzer asset из NuGet
не передаётся: подключите проект генератора отдельно как analyzer, как в
[MinimalPolling.csproj](../samples/TelegramBotKit.Sample.MinimalPolling/TelegramBotKit.Sample.MinimalPolling.csproj).
Для обычного NuGet-потребителя отдельная ссылка на генератор не нужна.
Подробнее: [генерация команд](generated-commands.md).

## 5. Проверьте lifetime и изменения поведения

Глобальные class-based middleware теперь по умолчанию регистрируются как scoped
и разрешаются из scope текущего update. Если middleware хранит общее состояние,
которое раньше жило в одном экземпляре, явно зарегистрируйте его как singleton
и обеспечьте потокобезопасность. Singleton не должен захватывать scoped-зависимости.
Существующая регистрация конкретного middleware в DI сохраняется.

`IUpdateDispatcher.DispatchAsync(Update, CancellationToken)` не изменился.
Если у вас есть собственный dispatcher или декоратор, проверьте новый необязательный
`IScheduledUpdateDispatcher`: декоратор должен передавать эту возможность дальше,
чтобы сохранить отдельный путь доставки ответов `WaitForUserResponse` при занятом
actor или лимите параллелизма. См. [Hosting](hosting.md#conversation-response-scheduling).

При остановке host отменяет и ожидает принятую работу. Обработчики должны учитывать
`BotContext.CancellationToken` и возвращать task всей операции. Приём остаётся
in-memory: продвижение polling offset означает приём update, а не успешное завершение
обработчика. Перезапуск не гарантирует повторную доставку уже принятых updates.

Методы отправки текста/фото по `Message` и `CallbackQuery`, а также reply теперь
наследуют `MessageThreadId`, если в запросе не указан другой. Проверьте ответы
в forum topics; если нужна отправка только по chat ID, используйте перегрузку
с явным `chatId`. Существующие интерфейсы команд и вызов `WaitForUserResponse`
переписывать не требуется; timeout и cancellation ожидания возвращают `null`.

## 6. Webhook подключайте отдельно, если он нужен

Для сохранения polling этот шаг пропустите. Для ASP.NET Core установите
`TelegramBotKit.Webhook.AspNetCore` версии `0.4.0`, подключите
`AddTelegramBotKitWebhook(...)` и `MapTelegramBotKitWebhook()` вместо polling.
Одного объекта `WebhookOptions` недостаточно для запуска приёма.

Регистрация webhook в Telegram выполняется явно через
`SetTelegramBotKitWebhookAsync()`; запуск и остановка приложения не меняют удалённую
настройку. Polling и webhook нельзя включать одновременно. При обратном переходе
на polling удалите удалённый webhook через `DeleteTelegramBotKitWebhookAsync()`.
Полная настройка и контракт HTTP-подтверждения описаны в [webhooks](webhooks.md).

## Проверка после обновления

1. Восстановите зависимости и пересоберите все проекты-потребители в Release.
   Убедитесь, что не осталось старого Generators и смешанных версий TelegramBotKit.
2. На тестовом боте проверьте `/start`, text-команду, callback и неизвестную команду
   с вашим fallback. Проверьте создание `Keyboard.Callback<THandler>()`, если используете его.
3. Отправьте и отредактируйте сообщение: Message и EditedMessage должны попасть
   в ожидаемые независимые маршруты. Проверьте локальные и глобальные middleware.
4. Пройдите диалог `WaitForUserResponse` при `MaxDegreeOfParallelism = 1`,
   включая успешный ответ и timeout. Проверьте ответы в forum topics, если они используются.
5. Остановите host во время обработки и убедитесь, что обработчики завершаются
   по отмене. Для webhook дополнительно проверьте secret и явную регистрацию.

Для ручной проверки исходников используйте
[сценарии ConsolePolling](samples/console-polling.md). `PhotoSelector` пока ссылается
на опубликованные preview-пакеты; его успешный запуск не подтверждает миграцию
на локальную реализацию 0.4.0.
