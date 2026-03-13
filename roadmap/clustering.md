# Кластеризация VibeMQ

**Статус:** `Not started`

Кластеризация в коде пока отсутствует: нет `UseClustering`, `ClusterOptions`, `IClusterCoordinator` и отдельных кластерных пакетов.

---

## Цель

Сделать несколько узлов брокера единым логическим брокером с прозрачной работой для клиентов:
- подключение к любому узлу;
- корректная доставка `RoundRobin` и `FanOut` между узлами;
- восстановление после падения узла без ручного вмешательства.

---

## Приоритетный backlog

### P0 — MVP кластеризации (обязательный минимум)

- [ ] Ввести базовые абстракции в `src/`:
  - `ClusterOptions`
  - `NodeInfo`
  - `ClusterEnvelope` / `ClusterMessageType`
  - `IClusterCoordinator`
  - `IClusterTransport`
- [ ] Добавить `BrokerBuilder.UseClustering(...)` и DI-подключение.
- [ ] Реализовать `TcpClusterTransport`:
  - discovery/handshake;
  - heartbeat;
  - peer reconnect.
- [ ] Реализовать координатор на shared storage (первый backend: PostgreSQL):
  - `cluster_nodes`;
  - `cluster_subscriptions`;
  - `message_claims`;
  - `fanout_ack_counters`.
- [ ] Интегрировать с `QueueManager` и `ConnectionManager`:
  - RoundRobin claim через coordinator;
  - FanOut пересылка между узлами;
  - учёт cluster subscriptions.
- [ ] Добавить failover:
  - детекция dead node;
  - release/requeue claims умершего узла.
- [ ] Интеграционные тесты: минимум 3 узла.

### P1 — Надёжность и эксплуатация

- [ ] Degraded mode при недоступном shared storage.
- [ ] Инвалидация permission cache между узлами.
- [ ] Базовые cluster-метрики:
  - peer count;
  - cross-node deliver rate;
  - claim contention;
  - heartbeat lag.
- [ ] Документация:
  - гайд по запуску кластера;
  - Docker Compose пример;
  - раздел в `docs/docs/changelog.rst`.

### P2 — Расширения

- [ ] `SqliteClusterCoordinator` (dev/single-machine).
- [ ] `VibeMQ.Server.Clustering.Redis`.
- [ ] Подсказки для клиента (мульти-host reconnect).
- [ ] K8s deployment examples.

---

## Технические решения (зафиксировано)

- Для `RoundRobin` использовать атомарный claim в shared storage.
- Для `FanOutWithAck` использовать distributed ACK counter в shared storage.
- Клиенты не обязаны знать топологию кластера.
- Первая версия: single region/LAN, без требований CP в сетевых партициях.

---

## DoD (Definition of Done)

- [ ] Публичный API кластеризации стабилен и покрыт тестами.
- [ ] 3-node интеграционный сценарий зелёный:
  - publish на узле A;
  - subscribe на узлах B/C;
  - корректный `RoundRobin`/`FanOut`.
- [ ] После падения узла сообщения не теряются и продолжают доставляться.
- [ ] Документация + RU-переводы обновлены.
