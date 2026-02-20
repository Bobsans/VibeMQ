# Интерфейс клиента для DI (помимо фабрики)

**Статус:** выполнено.

**Описание:** Регистрация `IVibeMQClient` в DI, чтобы его можно было инжектить в классы и использовать сразу для отправки сообщений. Клиент должен быть уже подключён — без необходимости вызывать `ConnectAsync` вручную (connection managed internally, lazy connect на первый вызов).

## Текущее состояние

- **VibeMQ.Client:** класс `VibeMQClient` с `ConnectAsync`, `PublishAsync`, `SubscribeAsync`, `UnsubscribeAsync`, `DisconnectAsync`, `IsConnected`; внутри уже есть переподключение, keep-alive, thread-safe вызовы.
- **VibeMQ.Client.DependencyInjection:** есть `IVibeMQClientFactory` и `VibeMQClientFactory` — фабрика создаёт подключённый `VibeMQClient`, вызывающий код сам делает `await using var client = await factory.CreateAsync()` и dispose.
- Интерфейса `IVibeMQClient` пока нет; план — ввести его в Client и реализовать managed-обёртку в DI-пакете.

## Варианты реализации

1. **ManagedVibeMQClient (рекомендуется)**  
   Отдельный класс в DI-пакете: держит `IVibeMQClientFactory` (или настройки + логгер), при первом вызове `PublishAsync`/`SubscribeAsync` вызывает `CreateAsync()`, сохраняет экземпляр `VibeMQClient` и дальше делегирует ему. Переподключение остаётся внутри `VibeMQClient`. Регистрируем как Singleton, один общий клиент на приложение.

2. **HostedService + общая ссылка на клиент**  
   HostedService при `StartAsync` создаёт клиент (или при первом запросе к обёртке) и кладёт в общий “holder”; сервисы получают через DI этот holder как `IVibeMQClient`. Shutdown — в `StopAsync` HostedService вызывает `DisposeAsync` у клиента. Плюс: корректный async lifecycle. Минус: сложнее, два компонента (HostedService + holder).

3. **Оставить только фабрику**  
   Не вводить `IVibeMQClient`, документировать паттерн “получи фабрику, создай клиент, await using”. Минус: не выполняется цель “инжектить и сразу использовать”.

**Вывод:** идём по варианту 1 (ManagedVibeMQClient), с явным решением проблемы async dispose (см. ниже).

## Потенциальные проблемы и решения

| Проблема | Решение |
|----------|--------|
| **Async dispose в синхронном DI** — Singleton владеет `VibeMQClient` (IAsyncDisposable), при остановке приложения контейнер вызывает `IDisposable.Dispose()`, а не `DisposeAsync`. | В `ManagedVibeMQClient` реализовать `IDisposable.Dispose()`: внутри вызвать sync-over-async для `DisposeAsync()` с ограничением по времени (например 5–10 с), чтобы не блокировать бесконечно. Альтернатива: отдельный HostedService, который в `StopAsync` вызывает `DisposeAsync` у клиента (тогда ManagedVibeMQClient только держит ссылку, не владеет lifecycle). |
| **Lazy connect: гонка при первом вызове** — несколько потоков одновременно вызывают Publish/Subscribe до первого подключения. | Один раз захватывать lock (или SemaphoreSlim), внутри проверять “уже создан ли клиент”; если нет — вызывать `CreateAsync()`, сохранять в поле, затем отпускать lock. Все последующие вызовы используют уже созданный экземпляр. |
| **Thread-safety после подключения** | Делегирование в один и тот же экземпляр `VibeMQClient`; он уже потокобезопасен (конкурентные вызовы Publish/Subscribe, внутренние словари, read loop). Доп. проверок не нужно. |
| **Singleton vs Scoped** | По умолчанию Singleton: одна общая подключение на приложение, меньше нагрузки на брокер. Scoped давал бы одно подключение на scope (например на запрос), что обычно избыточно. При необходимости можно добавить перегрузку `AddVibeMQClient(..., ServiceLifetime.Scoped)`. |
| **Возврат из SubscribeAsync** | Интерфейс возвращает `Task<IAsyncDisposable>` — вызывающий код делает `await using var sub = await client.SubscribeAsync(...)`. Unsubscribe через `DisposeAsync`; метода `UnsubscribeAsync` в интерфейсе не требуется. |
| **Расположение IVibeMQClient** | Интерфейс в `VibeMQ.Client` (без ссылки на DI). `VibeMQClient` реализует `IVibeMQClient`. В DI-пакете только `ManagedVibeMQClient : IVibeMQClient`. Циклических зависимостей нет. |

## Детальный план

## Шаг 1: Создание интерфейса IVibeMQClient
**Файл:** `src/VibeMQ.Client/IVibeMQClient.cs`

**Задачи:**
1. Создать интерфейс `IVibeMQClient` с методами:
   ```csharp
   public interface IVibeMQClient {
       Task PublishAsync<T>(string queueName, T payload, CancellationToken cancellationToken = default);
       Task<IAsyncDisposable> SubscribeAsync<T>(string queueName, Func<T, Task> handler, CancellationToken cancellationToken = default);
       bool IsConnected { get; }
   }
   ```
2. Реализовать интерфейс в `VibeMQClient`

## Шаг 2: Реализация managed клиента
**Файл:** `src/VibeMQ.Client.DependencyInjection/ManagedVibeMQClient.cs`

**Задачи:**
1. Создать класс `ManagedVibeMQClient : IVibeMQClient, IDisposable` (при необходимости также `IAsyncDisposable` для явного await DisposeAsync).
2. Lazy connection: при первом вызове Publish/Subscribe под lock вызывать `IVibeMQClientFactory.CreateAsync()`, сохранять экземпляр `VibeMQClient`, делегировать ему все вызовы.
3. Управление жизненным циклом:
   - Подключение при первом использовании;
   - Переподключение при разрыве — не реализовывать в ManagedVibeMQClient, этим уже занимается `VibeMQClient`;
   - Graceful shutdown: в `IDisposable.Dispose()` вызывать sync-over-async для `VibeMQClient.DisposeAsync()` с таймаутом (например 5–10 с), чтобы при остановке приложения соединение корректно закрывалось.
4. Thread-safe: один раз инициализировать клиент под lock; далее все вызовы — к одному экземпляру (VibeMQClient потокобезопасен).

## Шаг 3: Обновление ServiceCollectionExtensions
**Файл:** `src/VibeMQ.Client.DependencyInjection/ServiceCollectionExtensions.cs`

**Задачи:**
1. Текущий метод `AddVibeMQClient()` уже регистрирует фабрику `IVibeMQClientFactory` и настройки. Дополнительно зарегистрировать `IVibeMQClient` → `ManagedVibeMQClient` (Singleton). Тогда один вызов `services.AddVibeMQClient(...)` даёт и фабрику, и инжектируемый клиент.
2. Регистрация `IVibeMQClient` как **Singleton** по умолчанию (одно подключение на приложение). При необходимости позже можно добавить перегрузку с `ServiceLifetime`.
3. `ManagedVibeMQClient` резолвит `IVibeMQClientFactory` (или настройки + логгер) и при первом обращении создаёт клиент через фабрику.

## Шаг 4: Тестирование
**Задачи:**
1. Тесты инжекции `IVibeMQClient`
2. Тесты lazy connection
3. Тесты переподключения
4. Тесты использования в сервисах

---

## Выполнено (дата реализации)

- **Шаг 1:** Интерфейс `IVibeMQClient` в [`src/VibeMQ.Client/IVibeMQClient.cs`](../src/VibeMQ.Client/IVibeMQClient.cs), реализация в `VibeMQClient`.
- **Шаг 2:** Класс [`ManagedVibeMQClient`](../src/VibeMQ.Client.DependencyInjection/ManagedVibeMQClient.cs) в DI-пакете: lazy connect под `SemaphoreSlim`, делегирование в `VibeMQClient`, `IDisposable` с sync-over-async и таймаутом 5 с.
- **Шаг 3:** В [`ServiceCollectionExtensions.cs`](../src/VibeMQ.Client.DependencyInjection/ServiceCollectionExtensions.cs) зарегистрирован `IVibeMQClient` → `ManagedVibeMQClient` (Singleton). Один вызов `AddVibeMQClient()` даёт и фабрику, и инжектируемый клиент.
- **Шаг 4:** Тесты — по желанию добавить отдельно (unit/интеграционные).

