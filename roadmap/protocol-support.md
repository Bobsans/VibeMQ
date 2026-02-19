# Поддержка других протоколов

**Описание:** Адаптеры для совместимости с существующими системами и стандартами.

**Детальный план:**

## AMQP 1.0 адаптер

### Шаг 1: Изучение спецификации AMQP 1.0
**Задачи:**
1. Изучить AMQP 1.0 спецификацию
2. Определить маппинг между VibeMQ и AMQP концепциями:
   - Queue → AMQP Node
   - Publish → AMQP Transfer
   - Subscribe → AMQP Link (Receiver)
   - ACK → AMQP Disposition

### Шаг 2: Создание проекта для AMQP адаптера
**Файл:** Новый проект `src/VibeMQ.Protocol.AMQP`

**Задачи:**
1. Создать проект `VibeMQ.Protocol.AMQP`
2. Установить библиотеку для AMQP (например, `AMQPNetLite`)
3. Создать базовую структуру адаптера

### Шаг 3: Реализация AMQP сервера
**Файл:** `src/VibeMQ.Protocol.AMQP/AmqpServer.cs`

**Задачи:**
1. Реализовать AMQP connection handling
2. Реализовать маппинг AMQP команд на VibeMQ команды:
   - `OPEN` → Connect
   - `TRANSFER` → Publish
   - `ATTACH` (Receiver) → Subscribe
   - `DISPOSITION` (accepted) → ACK
3. Обработка AMQP sessions и links
4. Поддержка AMQP transactions (опционально)

### Шаг 4: Реализация AMQP клиента
**Файл:** `src/VibeMQ.Client.AMQP/AmqpVibeMQClient.cs`

**Задачи:**
1. Создать проект `VibeMQ.Client.AMQP`
2. Реализовать клиент, совместимый с `IVibeMQClient` интерфейсом
3. Использовать AMQP протокол вместо native VibeMQ протокола

### Шаг 5: Тестирование
**Задачи:**
1. Интеграционные тесты с RabbitMQ (если возможно)
2. Тесты совместимости
3. Performance тесты

---

## MQTT адаптер

### Шаг 1: Изучение MQTT спецификации
**Задачи:**
1. Изучить MQTT 3.1.1 и 5.0 спецификации
2. Определить маппинг:
   - Topic → Queue
   - PUBLISH → Publish
   - SUBSCRIBE → Subscribe
   - QoS levels → Delivery modes

### Шаг 2: Создание проекта
**Файл:** Новый проект `src/VibeMQ.Protocol.MQTT`

**Задачи:**
1. Создать проект
2. Установить MQTT библиотеку (например, `MQTTnet`)

### Шаг 3: Реализация MQTT сервера
**Файл:** `src/VibeMQ.Protocol.MQTT/MqttServer.cs`

**Задачи:**
1. Реализовать MQTT broker:
   - CONNECT/CONNACK
   - PUBLISH/PUBACK/PUBREC/PUBREL/PUBCOMP
   - SUBSCRIBE/SUBACK
   - UNSUBSCRIBE/UNSUBACK
   - DISCONNECT
2. Поддержка QoS 0, 1, 2
3. Поддержка retained messages
4. Поддержка will messages
5. Topic wildcards (+ и #)

### Шаг 4: Тестирование
**Задачи:**
1. Тесты с MQTT клиентами (mosquitto_pub/sub)
2. Тесты QoS уровней
3. Performance тесты

---

## HTTP REST API

### Шаг 1: Проектирование API
**Документ:** `docs/docs/http-api.rst`

**Задачи:**
1. Определить REST endpoints:
   - `POST /api/v1/queues/{queue}/messages` - публикация
   - `GET /api/v1/queues/{queue}/messages` - получение (polling)
   - `POST /api/v1/queues/{queue}/messages/{id}/ack` - подтверждение
   - `GET /api/v1/queues` - список очередей
   - `POST /api/v1/queues` - создание очереди
   - `DELETE /api/v1/queues/{queue}` - удаление очереди
2. Определить формат запросов/ответов (JSON)
3. Определить аутентификацию (Bearer token)

### Шаг 2: Создание проекта
**Файл:** Новый проект `src/VibeMQ.Server.Http`

**Задачи:**
1. Создать проект с ASP.NET Core
2. Настроить минимальные API или контроллеры

### Шаг 3: Реализация контроллеров
**Файл:** `src/VibeMQ.Server.Http/Controllers/QueuesController.cs`
**Файл:** `src/VibeMQ.Server.Http/Controllers/MessagesController.cs`

**Задачи:**
1. Реализовать endpoints для очередей
2. Реализовать endpoints для сообщений
3. Интеграция с `IQueueManager`
4. Обработка ошибок и валидация
5. Swagger/OpenAPI документация

### Шаг 4: Аутентификация
**Задачи:**
1. Интеграция с существующей системой аутентификации
2. Middleware для проверки токенов

### Шаг 5: Тестирование
**Задачи:**
1. Unit тесты контроллеров
2. Integration тесты через HTTP клиент
3. Тесты производительности

---

## WebSocket поддержка

### Шаг 1: Проектирование протокола
**Задачи:**
1. Определить формат сообщений через WebSocket (JSON)
2. Определить команды (аналогично native протоколу)
3. Поддержка бинарных сообщений (опционально)

### Шаг 2: Реализация WebSocket сервера
**Файл:** `src/VibeMQ.Server.WebSocket/WebSocketServer.cs`

**Задачи:**
1. Использовать ASP.NET Core WebSockets
2. Адаптировать существующий протокол для WebSocket
3. Обработка подключений/отключений
4. Keep-alive через ping/pong

### Шаг 3: WebSocket клиент
**Файл:** `src/VibeMQ.Client.WebSocket/WebSocketVibeMQClient.cs`

**Задачи:**
1. Создать клиент для браузеров (JavaScript/TypeScript)
2. Или адаптер для .NET клиента через WebSocket

### Шаг 4: Тестирование
**Задачи:**
1. Тесты с браузерными клиентами
2. Тесты производительности
