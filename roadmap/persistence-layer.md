# Persistence слой

**Описание:** Добавление постоянного хранилища сообщений для сохранения данных между перезапусками сервера.

**Текущее состояние:**
- Существует интерфейс `IMessageStore` в `VibeMQ.Core.Interfaces`
- Реализован `InMemoryMessageStore` в `VibeMQ.Server.Storage`
- Сообщения хранятся только в памяти

**Детальный план:**

## Шаг 1: Расширение интерфейса IMessageStore
**Файл:** `src/VibeMQ.Core/Interfaces/IMessageStore.cs`

**Задачи:**
1. Добавить методы из роадмапа:
   ```csharp
   Task InitializeAsync();
   Task BackupAsync(string path);
   Task RestoreAsync(string path);
   Task CompactAsync(); // Для журнальных структур
   ```
2. Создать новый интерфейс `IPersistentMessageStore : IMessageStore` для разделения ответственности
3. Обновить существующие реализации для поддержки новых методов (InMemory может иметь пустые реализации)

## Шаг 2: Реализация SQLite хранилища
**Файл:** `src/VibeMQ.Server.Storage/SqliteMessageStore.cs`
**Проект:** `VibeMQ.Server` (или новый `VibeMQ.Server.Storage.Sqlite`)

**Задачи:**
1. Создать проект/пакет для SQLite реализации (опционально)
2. Установить NuGet пакет: `Microsoft.Data.Sqlite`
3. Реализовать `IPersistentMessageStore`:
   - Схема БД: таблицы `Messages`, `Queues`, `Subscriptions`
   - Индексы на `QueueName`, `Timestamp`, `MessageId`
   - Методы: `InitializeAsync` (создание схемы), `AddAsync`, `GetAsync`, `RemoveAsync`, `GetPendingAsync`
   - Реализовать `BackupAsync` (SQLite backup API)
   - Реализовать `RestoreAsync` (восстановление из backup)
   - `CompactAsync` может вызывать `VACUUM` для SQLite
4. Обработка транзакций для атомарности операций
5. Миграции схемы БД (если потребуется в будущем)

## Шаг 3: Реализация RocksDB хранилища
**Файл:** `src/VibeMQ.Server.Storage/RocksDbMessageStore.cs`
**Проект:** Новый пакет `VibeMQ.Server.Storage.RocksDB`

**Задачи:**
1. Создать новый проект `VibeMQ.Server.Storage.RocksDB`
2. Установить NuGet пакет: `RocksDbNative` или `RocksDbSharp`
3. Реализовать `IPersistentMessageStore`:
   - Использовать Column Families для организации данных (Messages, Queues)
   - Ключи: `MessageId`, составные ключи для `QueueName:Timestamp:MessageId`
   - Batch writes для производительности
   - Реализовать `BackupAsync` (RocksDB backup API)
   - Реализовать `RestoreAsync`
   - `CompactAsync` через `CompactRange`
4. Настройка опций RocksDB (write buffer, compression, etc.)

## Шаг 4: Реализация PostgreSQL хранилища
**Файл:** `src/VibeMQ.Server.Storage/PostgreSqlMessageStore.cs`
**Проект:** Новый пакет `VibeMQ.Server.Storage.PostgreSQL`

**Задачи:**
1. Создать новый проект `VibeMQ.Server.Storage.PostgreSQL`
2. Установить NuGet пакет: `Npgsql`
3. Реализовать `IPersistentMessageStore`:
   - Схема БД: таблицы `messages`, `queues`, `subscriptions`
   - Использовать JSONB для хранения payload
   - Индексы: GIN для JSONB, B-tree для `queue_name`, `timestamp`
   - Партиционирование по `queue_name` (опционально, для больших нагрузок)
   - Реализовать `BackupAsync` (pg_dump или streaming replication)
   - Реализовать `RestoreAsync` (pg_restore)
   - `CompactAsync` может быть `VACUUM ANALYZE`
4. Connection pooling через Npgsql
5. Поддержка транзакций

## Шаг 5: Конфигурация и выбор хранилища
**Файл:** `src/VibeMQ.Core/Configuration/BrokerOptions.cs`
**Файл:** `src/VibeMQ.Server/BrokerBuilder.cs`

**Задачи:**
1. Добавить в `BrokerOptions`:
   ```csharp
   public StorageType StorageType { get; set; } = StorageType.InMemory;
   public string? StorageConnectionString { get; set; }
   public StorageOptions? StorageOptions { get; set; }
   ```
2. Создать enum `StorageType` (InMemory, Sqlite, RocksDB, PostgreSQL)
3. Создать класс `StorageOptions` с настройками для каждого типа
4. Обновить `BrokerBuilder` для регистрации правильного хранилища
5. Добавить extension методы: `UseSqliteStorage()`, `UseRocksDbStorage()`, `UsePostgreSqlStorage()`

## Шаг 6: Интеграция с QueueManager
**Файл:** `src/VibeMQ.Server/Queues/QueueManager.cs`

**Задачи:**
1. Обновить `QueueManager` для работы с persistent хранилищем
2. При старте: загрузить pending сообщения из хранилища
3. При публикации: сохранять в хранилище перед добавлением в очередь
4. При ACK: удалять из хранилища
5. При перезапуске: восстановление состояния очередей из хранилища

## Шаг 7: Тестирование
**Файл:** `tests/VibeMQ.Tests.Integration/PersistenceTests.cs`

**Задачи:**
1. Unit тесты для каждого типа хранилища
2. Integration тесты: публикация → перезапуск → проверка восстановления
3. Тесты производительности (benchmarks)
4. Тесты backup/restore

