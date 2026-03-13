# Поддержка других протоколов

**Статус:** `Not started`

В кодовой базе пока нет проектов:
- `src/VibeMQ.Protocol.AMQP`
- `src/VibeMQ.Protocol.MQTT`
- `src/VibeMQ.Server.Http`
- `src/VibeMQ.Server.WebSocket`

---

## Приоритизация

### P0 — HTTP API (самый быстрый путь к интеграциям)

- [ ] Создать `src/VibeMQ.Server.Http` (ASP.NET Core Minimal API).
- [ ] Реализовать v1 endpoints:
  - `POST /api/v1/queues/{queue}/messages`
  - `GET /api/v1/queues/{queue}/messages`
  - `POST /api/v1/queues/{queue}/messages/{id}/ack`
  - `GET /api/v1/queues`
  - `POST /api/v1/queues`
  - `DELETE /api/v1/queues/{queue}`
- [ ] Интеграция с текущими `IQueueManager`/`BrokerServer` API.
- [ ] Auth для HTTP (Bearer или reuse текущей auth модели).
- [ ] OpenAPI/Swagger.
- [ ] Документация: `docs/docs/http-api.rst` + `docs/docs/changelog.rst` + RU переводы.

### P1 — WebSocket transport

- [ ] Создать `src/VibeMQ.Server.WebSocket`.
- [ ] Определить WS framing (JSON first, binary optional later).
- [ ] Реализовать команды connect/publish/subscribe/ack/ping.
- [ ] Добавить reconnect/heartbeat semantics.
- [ ] Клиент:
  - либо `src/VibeMQ.Client.WebSocket` для .NET;
  - либо JS/TS SDK для браузера.
- [ ] Интеграционные тесты с браузерным клиентом.

### P2 — MQTT adapter

- [ ] Создать `src/VibeMQ.Protocol.MQTT`.
- [ ] Библиотека: `MQTTnet`.
- [ ] MVP:
  - CONNECT/CONNACK
  - PUBLISH (QoS 0/1)
  - SUBSCRIBE/SUBACK
  - DISCONNECT
- [ ] Маппинг topic → queue и wildcard (`+`, `#`).
- [ ] Тесты с `mosquitto_pub/sub`.

### P3 — AMQP 1.0 adapter

- [ ] Создать `src/VibeMQ.Protocol.AMQP`.
- [ ] Библиотека: `AMQPNetLite`.
- [ ] MVP:
  - OPEN
  - TRANSFER
  - ATTACH (receiver)
  - DISPOSITION (accepted)
- [ ] Совместимость с базовым queue semantics VibeMQ.
- [ ] Интеграционные тесты.

---

## Ключевые решения (чтобы не спорить заново)

- Делать поэтапно: сначала `HTTP`, затем `WebSocket`, и только потом `MQTT/AMQP`.
- Для каждого протокола сначала MVP (ограниченный набор команд), потом расширение.
- Не ломать существующий native протокол; новые протоколы — отдельные адаптеры/пакеты.

---

## DoD для каждого протокола

- [ ] Публичный API/контракт задокументирован.
- [ ] Есть минимум 1 e2e сценарий publish + subscribe + ack.
- [ ] Ошибки/валидация/аутентификация покрыты тестами.
- [ ] Обновлены `docs/docs/changelog.rst` и RU `.po`.
