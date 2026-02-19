# Интерфейс клиента для DI (помимо фабрики)

**Описание:** Регистрация `IVibeMQClient` в DI, чтобы его можно было инжектить в классы и использовать сразу для отправки сообщений. Клиент должен быть уже подключён — без необходимости вызывать `ConnectAsync` вручную (connection managed internally, lazy connect на первый вызов).

**Детальный план:**

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
1. Создать класс `ManagedVibeMQClient : IVibeMQClient`
2. Lazy connection: подключение при первом вызове
3. Управление жизненным циклом:
   - Подключение при первом использовании
   - Переподключение при разрыве соединения
   - Graceful shutdown при dispose
4. Thread-safe реализация

## Шаг 3: Обновление ServiceCollectionExtensions
**Файл:** `src/VibeMQ.Client.DependencyInjection/ServiceCollectionExtensions.cs`

**Задачи:**
1. Добавить метод `AddVibeMQClient()` который регистрирует `IVibeMQClient`:
   ```csharp
   services.AddVibeMQClient(options => { ... }); // Регистрирует IVibeMQClient
   ```
2. Регистрация как Singleton или Scoped (в зависимости от требований)
3. Использование `ManagedVibeMQClient` как реализации

## Шаг 4: Тестирование
**Задачи:**
1. Тесты инжекции `IVibeMQClient`
2. Тесты lazy connection
3. Тесты переподключения
4. Тесты использования в сервисах

