# Кластеризация

**Описание:** Поддержка объединения нескольких узлов VibeMQ в кластер для горизонтального масштабирования и отказоустойчивости.

**Детальный план:**

## Шаг 1: Архитектура кластера
**Документ:** `docs/docs/clustering.rst`

**Задачи:**
1. Определить архитектуру:
   - Leader-follower модель или peer-to-peer
   - Механизм выбора лидера (Raft, Paxos, или простой majority voting)
   - Репликация сообщений (синхронная/асинхронная)
   - Согласованность данных (strong/eventual consistency)
2. Определить протокол обмена между узлами
3. Определить метрики и health checks для узлов

## Шаг 2: Discovery механизм
**Файл:** `src/VibeMQ.Core/Interfaces/IClusterDiscovery.cs`
**Файл:** `src/VibeMQ.Server/Cluster/ClusterDiscovery.cs`

**Задачи:**
1. Создать интерфейс `IClusterDiscovery`:
   ```csharp
   Task<IReadOnlyList<ClusterNode>> DiscoverNodesAsync();
   Task RegisterNodeAsync(ClusterNode node);
   Task UnregisterNodeAsync(string nodeId);
   ```
2. Реализовать варианты discovery:
   - **Static**: список узлов из конфигурации
   - **DNS**: через SRV записи
   - **Consul/etcd**: через service discovery
   - **Kubernetes**: через Kubernetes API
3. Создать базовый класс `BaseClusterDiscovery`

## Шаг 3: Leader Election
**Файл:** `src/VibeMQ.Server/Cluster/LeaderElection.cs`

**Задачи:**
1. Реализовать алгоритм выбора лидера:
   - Простой вариант: majority voting с таймаутами
   - Продвинутый: Raft consensus algorithm
2. Обработка сетевых разделений (split-brain)
3. Автоматический перевыбор при падении лидера
4. Heartbeat между узлами

## Шаг 4: Репликация сообщений
**Файл:** `src/VibeMQ.Server/Cluster/MessageReplication.cs`

**Задачи:**
1. Определить стратегию репликации:
   - Синхронная: ждать подтверждения от N узлов
   - Асинхронная: fire-and-forget с eventual consistency
2. Реализовать протокол репликации:
   - Отправка сообщений на follower узлы
   - Подтверждение получения (ACK от followers)
   - Обработка конфликтов версий
3. Репликация состояния очередей и подписок
4. Обработка откатов при сбоях

## Шаг 5: Синхронизация состояния
**Файл:** `src/VibeMQ.Server/Cluster/StateSynchronization.cs`

**Задачи:**
1. Синхронизация очередей между узлами
2. Синхронизация подписок
3. Синхронизация метрик и статистики
4. Gossip протокол для распространения состояния (опционально)

## Шаг 6: Конфигурация кластера
**Файл:** `src/VibeMQ.Core/Configuration/ClusterOptions.cs`
**Файл:** `src/VibeMQ.Server/BrokerBuilder.cs`

**Задачи:**
1. Создать `ClusterOptions`:
   ```csharp
   public class ClusterOptions {
       public bool Enabled { get; set; }
       public DiscoveryMode DiscoveryMode { get; set; }
       public int ReplicationFactor { get; set; } = 3;
       public ConsistencyLevel ConsistencyLevel { get; set; } = ConsistencyLevel.Quorum;
       public string? NodeId { get; set; }
       public List<string>? SeedNodes { get; set; }
   }
   ```
2. Добавить в `BrokerOptions`
3. Обновить `BrokerBuilder` для настройки кластера

## Шаг 7: Сетевой протокол для кластера
**Файл:** `src/VibeMQ.Protocol/Cluster/ClusterProtocol.cs`

**Задачи:**
1. Определить протокол обмена между узлами:
   - Команды: `REPLICATE`, `SYNC`, `HEARTBEAT`, `ELECTION`
   - Формат сообщений (JSON или бинарный)
2. Реализовать сериализацию/десериализацию
3. Обработка ошибок и таймаутов

## Шаг 8: Интеграция с BrokerServer
**Файл:** `src/VibeMQ.Server/BrokerServer.cs`

**Задачи:**
1. Инициализация кластера при старте сервера
2. Маршрутизация сообщений через кластер
3. Обработка запросов от других узлов
4. Graceful shutdown с передачей лидерства

## Шаг 9: Тестирование
**Файл:** `tests/VibeMQ.Tests.Integration/ClusterTests.cs`

**Задачи:**
1. Тесты для discovery механизмов
2. Тесты для leader election
3. Тесты для репликации (с потерей узлов)
4. Тесты для сетевых разделений
5. Load тесты кластера

