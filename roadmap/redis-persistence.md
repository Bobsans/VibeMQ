# Persistence провайдер на Redis

**Описание:** Хранилище сообщений и метаданных брокера на базе Redis как альтернативный `IStorageProvider` с упором на скорость и простоту развёртывания.

Связанные типы: `IStorageProvider` (`src/VibeMQ.Core/Interfaces/IStorageProvider.cs`), модели очередей/сообщений в Core (`src/VibeMQ.Core`), конфигурация брокера через `BrokerBuilder` и DI (`src/VibeMQ.Server`, `src/VibeMQ.Server.DI`).

---

## 1. Цели и сценарии

- **Быстрый in-memory + persistence backend**: минимальная латентность операций очереди при условии, что Redis развёрнут локально или в той же сети.
- **Простое продакшн‑развёртывание**: возможность использовать управляемый Redis (Azure Cache for Redis, Redis Enterprise и т.п.) вместо PostgreSQL/SQLite.
- **Совместимость с существующим API**: реализация `IStorageProvider` без изменений публичных контрактов брокера.
- **Поддержка базовых delivery‑mode**: RoundRobin, FanOut (с/без ACK) с сохранением текущих гарантий (at‑least‑once, exactly‑once там, где уже реализовано поверх хранилища).
- **Подготовка к кластеризации**: совместимость с `VibeMQ.Server.Clustering.Redis` (общая Redis‑инфраструктура для хранения состояния и кворумных операций).

Не цели первой версии:

- Полная транзакционная модель как у PostgreSQL (multi‑queue ACID транзакции).
- Сложные сценарии архивации/ретеншна истории сообщений (ограничимся TTL/limit на уровне очереди).

---

## 2. Выбор модели данных в Redis

### 2.1 Основные сущности

Для каждой очереди:

- **Метаданные очереди**: настройки, счётчики, служебная информация.
- **Сообщения**: тело + заголовки + служебные поля (delivery‑attempts, createdAt, priority и т.д.).
- **Индексы доставки**: структуры для RoundRobin/FanOut и пере‑доставки.

### 2.2 Ключи и структуры Redis (первый вариант)

Предлагаемый layout (префиксы условные, задаются через `RedisStorageOptions`):

- `vibemq:q:{queue}:meta` — `HASH` с метаданными очереди (DLQ, maxSize, ttlSeconds и т.п.).
- `vibemq:q:{queue}:seq` — `STRING` (инкрементный счётчик message‑seq для очереди).
- `vibemq:q:{queue}:pending` — `LIST` с ID сообщений в порядке доставки (основной «журнал» для RoundRobin/FanOut).
- `vibemq:m:{messageId}` — `HASH` с полным содержимым сообщения (headers, body, priority, timestamps).
- `vibemq:q:{queue}:inflight` — `HASH` или `ZSET` для трекинга сообщений «в полёте»:
  - key = messageId, value = timestamp/attempts/subscriberId.
- Дополнительно (опционально в v1): `ZSET` для приоритезации (`vibemq:q:{queue}:priority`).

Альтернатива — Redis Streams:

- `vibemq:stream:{queue}` — `XADD` / `XREADGROUP` для очередей с group‑based потребителями.

Для первой реализации выберем **простую модель LIST + HASH**, так как она ближе к существующему интерфейсу `IStorageProvider` (enqueue/dequeue/ack) и проще отлаживается. Streams можно добавить отдельным режимом позже.

---

## 3. Архитектура Redis‑провайдера

### 3.1 Новый проект

- Новый проект: `src/VibeMQ.Persistence.Redis/`
  - Зависимости:
    - `VibeMQ.Core` (для моделей/интерфейсов).
    - `StackExchange.Redis` (основной клиент к Redis).
  - Основные классы:
    - `RedisStorageProvider` — реализация `IStorageProvider`.
    - `RedisStorageOptions` — настройки подключения и layout ключей.
    - `RedisStorageConnectionFactory` — управление `ConnectionMultiplexer`.

### 3.2 Конфигурация (`RedisStorageOptions`)

Поля (минимально):

- `ConnectionString` — строка подключения к Redis (`host:port`, Sentinel/cluster URI и т.п.).
- `Database` — номер базы (по умолчанию 0).
- `KeyPrefix` — общий префикс (`"vibemq"` по умолчанию).
- `DefaultQueueTtl` — TTL в секундах для сообщений (0 = без TTL, управляется брокером).
- `UseFireAndForgetForNonCritical` — флаг использования Fire‑and‑Forget команд для некритичных операций (метрики/счётчики).

Интеграция:

- Через `BrokerBuilder`:
  - `UseRedisStorage(string connectionString, Action<RedisStorageOptions>? configure = null)`.
- Через DI‑пакет сервера:
  - `AddRedisStorage(this VibeMQBrokerBuilder builder, IConfiguration config)` для чтения опций из конфигурации.

---

## 4. Маппинг операций `IStorageProvider` на Redis

**Предположение:** текущий `IStorageProvider` позволяет:

- Создавать/обновлять/удалять очереди и их метаданные.
- Сохранять сообщение и связывать его с очередью.
- Получать следующее сообщение для доставки (RoundRobin / FanOut).
- Отмечать сообщение как подтверждённое (ACK) или для повторной доставки (NACK / retry).
- Получать информацию о размере очереди и базовой статистике.

### 4.1 Создание/удаление очереди

- `CreateQueueAsync`:
  - Создание `HASH` `vibemq:q:{queue}:meta` с настройками.
  - Инициализация `STRING` `vibemq:q:{queue}:seq` (0).
- `DeleteQueueAsync`:
  - Удаление всех ключей по шаблону `vibemq:q:{queue}:*` + всех сообщений, которые принадлежат этой очереди.
  - Для массового удаления сообщений:
    - либо храним `SET` `vibemq:q:{queue}:messages` с ID, либо
    - помечаем сообщения TTL и удаляем лениво (упрощённый вариант в v1).

### 4.2 Enqueue сообщения

Шаги:

1. Генерация `messageId` (GUID или `{queue}:{seq}` через `INCR` на `vibemq:q:{queue}:seq`).
2. Запись содержимого сообщения в `HASH` `vibemq:m:{messageId}`.
3. Добавление `messageId` в `LIST` `vibemq:q:{queue}:pending` через `RPUSH`.

Опционально: обновление счётчиков (размер очереди, последние N сообщений) в `meta`.

### 4.3 Dequeue / Peek для доставки

RoundRobin (упрощённый вариант):

- Используем `BLMOVE` / `RPOPLPUSH`:
  - `BLMOVE pending -> inflight` (правый → левый) с таймаутом ~0/short.
  - `inflight` в Redis — `LIST` с ID сообщений, а фактические данные хранятся в `HASH` по `messageId`.
  - На стороне брокера `inflight` также дублируется в памяти (для таймаутов/повторной доставки).

FanOut:

- Хранение сообщения в `HASH` + счётчик подписчиков/ACK в памяти + Redis‑ключ:
  - `vibemq:q:{queue}:fanout:{messageId}:expected` / `ackCount`.
- Для v1 допустимо повторно использовать существующую логику FanOut поверх Redis, не делая отдельной схемы.

### 4.4 ACK / NACK

- ACK:
  - Удаление `messageId` из `LIST`/`HASH` `inflight` для очереди (локально и в Redis).
  - Удаление `HASH` `vibemq:m:{messageId}` (если не требуется хранить историю).
  - Обновление счётчиков (completed, lastAckAt и т.п.).
- NACK / retry:
  - Перемещение `messageId` обратно в `pending` (`LPUSH`/`RPUSH` в зависимости от политики).
  - Обновление попыток доставки в `HASH` сообщения.

### 4.5 Метаданные и статистика

- Размер очереди: `LLEN` для `pending` + размер `inflight` (если входит в счёт).
- Время жизни сообщений: TTL на `HASH` сообщения либо управление ретеншном на уровне очереди (maxSize → `LTRIM`).

---

## 5. Конфигурация и интеграция с брокером

### 5.1 `BrokerBuilder` / сервер DI

Планируемые расширения:

- В `BrokerBuilder` (`src/VibeMQ.Server/BrokerBuilder.cs` или аналог):
  - `UseRedisStorage(string connectionString, Action<RedisStorageOptions>? configure = null)`.
- В DI‑слое (`src/VibeMQ.Server.DI`):
  - `AddRedisStorage(this VibeMQBrokerBuilder builder, IConfiguration configuration)`:
    - Читает секцию `VibeMQ:Storage:Redis`.
    - Настраивает `RedisStorageOptions`.
    - Регистрирует `RedisStorageProvider` как `IStorageProvider`.

### 5.2 Docker и документация

- Обновить `docker-compose`/докер‑примеры после реализации:
  - Добавить сервис `redis` рядом с брокером.
  - Прописать переменные окружения для подключения брокера к Redis.
- Обновить документацию (`docs/docs/`):
  - Раздел «Persistence backends»: Redis как опциональный провайдер, ограничения и рекомендации (одиночный инстанс, Sentinel, кластер).

---

## 6. Этапы реализации

### Этап 1 — Подготовка и skeleton проекта ✅

**Файлы/проекты:** `src/VibeMQ.Server.Storage.Redis/` (проект назван в стиле Sqlite).

**Реализовано:** проект, `RedisStorageOptions`, `RedisStorageConnectionFactory`, регистрация в `BrokerBuilder` и DI.

### Этап 2 — Базовые операции очередей и сообщений ✅

**Реализовано:** один класс `RedisStorageProvider` с созданием/удалением очередей (SET + HASH meta, LIST pending), сохранение/получение/удаление сообщений (HASH + LIST), DLQ (LIST + HASH). Брокер использует тот же write-ahead паттерн; dequeue/ACK выполняются в памяти брокера, провайдер даёт только persistence и восстановление при старте.

### Этап 3 — Надёжность и повторная доставка

**Задачи:**

1. Добавить трекинг in‑flight сообщений с таймаутами:
   - Redis‑структуры для хранения timestamp и попыток.
   - Фоновая задача в брокере или провайдере для ре‑enqueue сообщений, у которых истёк ack‑timeout.
2. Настроить политику retry (максимальное число попыток, переезд в DLQ).
3. Протестировать поведение при падении брокера/Redis:
   - перезапуск брокера при живом Redis;
   - перезапуск Redis (допустимая потеря данных в рамках выбранной конфигурации).

### Этап 4 — Производительность и оптимизации

**Задачи:**

1. Добавить batching для операций, где это возможно (массовые ACK, массовые удаления).
2. Минимизировать round‑trip’ы (pipeline/`ITransaction` `StackExchange.Redis`).
3. Прогнать нагрузочные тесты (публикация/доставка N сообщений/сек) и сравнить с существующим провайдером.
4. Задокументировать рекомендуемые настройки Redis (maxmemory, RDB/AOF, репликация).

### Этап 5 — Интеграция с кластеризацией (опционально)

**Идея:** при наличии `VibeMQ.Server.Clustering.Redis` использовать общий Redis‑кластер:

- Проверить, что layout ключей persistence не конфликтует с ключами, используемыми `IClusterCoordinator` на Redis.
- Выделить отдельные префиксы (`vibemq:persistence:*` и `vibemq:cluster:*`).
- Описать в документации рекомендуемую топологию (мастер + реплики, Sentinel, кластерный режим).

---

## 7. Критерии готовности

- Реализован `RedisStorageProvider`, удовлетворяющий текущему контракту `IStorageProvider`.
- Брокер может работать только с Redis‑хранилищем (без других провайдеров) во всех базовых сценариях:
  - создание/удаление очередей;
  - публикация/доставка/ACK/NACK;
  - повторная доставка после таймаута ACK.
- Нагрузочные тесты показывают сопоставимую или лучшую производительность, чем у существующего провайдера, при аналогичной конфигурации железа.
- Документация обновлена (новый раздел «Redis persistence provider» с примерами конфигурации).
- Добавлены примеры конфигурации (Docker/`appsettings.json`) для использования Redis в dev и prod.

