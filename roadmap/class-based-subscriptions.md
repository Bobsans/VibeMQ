# Class-based подписки

**Описание:** Подписка на сообщения через классы-обработчики вместо ручной регистрации колбэков.

**Детальный план:**

## Шаг 1: Проектирование API
**Задачи:**
1. Определить интерфейс `IMessageHandler<T>`:
   ```csharp
   public interface IMessageHandler<T> {
       Task HandleAsync(T message, CancellationToken cancellationToken);
   }
   ```
2. Определить способ регистрации:
   ```csharp
   broker.Subscribe<OrderCreated, OrderHandler>();
   ```

## Шаг 2: Создание интерфейсов
**Файл:** `src/VibeMQ.Core/Interfaces/IMessageHandler.cs`

**Задачи:**
1. Создать интерфейс `IMessageHandler<T>`
2. Определить базовые типы

## Шаг 3: Реализация подписки через классы
**Файл:** `src/VibeMQ.Server/Subscriptions/ClassBasedSubscriptionManager.cs`

**Задачи:**
1. Создать менеджер подписок на основе классов
2. Регистрация обработчиков:
   - Автоматическое определение типа сообщения из generic параметра
   - Создание экземпляра обработчика (через DI или Activator)
   - Регистрация подписки на очередь
3. Вызов обработчика при получении сообщения:
   - Десериализация сообщения в тип T
   - Вызов `HandleAsync` на обработчике
   - Обработка ошибок

## Шаг 4: Интеграция с DI
**Файл:** `src/VibeMQ.Server.DependencyInjection/ServiceCollectionExtensions.cs`

**Задачи:**
1. Добавить метод `AddMessageHandlers()` для регистрации обработчиков
2. Автоматическое сканирование сборки для поиска обработчиков
3. Регистрация обработчиков в DI контейнере

## Шаг 5: Обновление BrokerBuilder
**Файл:** `src/VibeMQ.Server/BrokerBuilder.cs`

**Задачи:**
1. Добавить метод `Subscribe<TMessage, THandler>()`
2. Интеграция с `ClassBasedSubscriptionManager`

## Шаг 6: Поддержка на клиенте (опционально)
**Файл:** `src/VibeMQ.Client/ClassBasedSubscriptions.cs`

**Задачи:**
1. Реализовать аналогичный API на клиенте
2. Регистрация обработчиков через DI

## Шаг 7: Тестирование
**Задачи:**
1. Тесты регистрации обработчиков
2. Тесты вызова обработчиков
3. Тесты обработки ошибок
4. Тесты с DI

