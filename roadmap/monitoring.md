# Расширенный мониторинг

**Описание:** Интеграция с популярными системами мониторинга и observability.

**Детальный план:**

## Prometheus метрики

### Шаг 1: Изучение Prometheus
**Задачи:**
1. Изучить формат Prometheus метрик
2. Определить какие метрики экспортировать:
   - `vibemq_messages_published_total` (counter)
   - `vibemq_messages_delivered_total` (counter)
   - `vibemq_messages_acked_total` (counter)
   - `vibemq_queue_size` (gauge)
   - `vibemq_connections_active` (gauge)
   - `vibemq_message_processing_duration_seconds` (histogram)

### Шаг 2: Создание проекта
**Файл:** Новый проект `src/VibeMQ.Metrics.Prometheus`

**Задачи:**
1. Создать проект
2. Установить `prometheus-net` или `prometheus-net.AspNetCore`

### Шаг 3: Реализация экспорта метрик
**Файл:** `src/VibeMQ.Metrics.Prometheus/PrometheusMetricsExporter.cs`

**Задачи:**
1. Создать экспортер метрик
2. Интегрировать с существующим `IBrokerMetrics`
3. Реализовать endpoint `/metrics` для Prometheus scraping
4. Добавить labels для метрик (queue_name, etc.)

### Шаг 4: Интеграция с BrokerServer
**Файл:** `src/VibeMQ.Server/BrokerBuilder.cs`

**Задачи:**
1. Добавить опцию для включения Prometheus метрик
2. Регистрация экспортера в DI

### Шаг 5: Тестирование
**Задачи:**
1. Тесты экспорта метрик
2. Интеграция с Prometheus сервером
3. Проверка формата метрик

---

## Grafana дашборды

### Шаг 1: Проектирование дашбордов
**Задачи:**
1. Определить панели:
   - Message throughput (rate)
   - Queue sizes
   - Connection count
   - Message latency (p50, p95, p99)
   - Error rates
   - DLQ size
2. Создать JSON конфигурацию дашборда

### Шаг 2: Создание дашбордов
**Файл:** `grafana/dashboards/vibemq-overview.json`

**Задачи:**
1. Создать Grafana дашборд JSON
2. Использовать Prometheus как источник данных
3. Добавить переменные (queue selection, time range)

### Шаг 3: Документация
**Документ:** `docs/docs/monitoring.rst`

**Задачи:**
1. Инструкции по настройке Grafana
2. Импорт дашборда
3. Настройка алертов

---

## Distributed tracing (OpenTelemetry)

### Шаг 1: Изучение OpenTelemetry
**Задачи:**
1. Изучить OpenTelemetry спецификацию
2. Определить трассировку:
   - Трассировка публикации сообщений
   - Трассировка доставки сообщений
   - Трассировка обработки сообщений клиентом

### Шаг 2: Создание проекта
**Файл:** Новый проект `src/VibeMQ.Tracing.OpenTelemetry`

**Задачи:**
1. Создать проект
2. Установить `OpenTelemetry` пакеты

### Шаг 3: Реализация трассировки
**Файл:** `src/VibeMQ.Tracing.OpenTelemetry/OpenTelemetryTracer.cs`

**Задачи:**
1. Создать ActivitySource для VibeMQ
2. Инструментировать код:
   - При публикации: создавать span
   - При доставке: продолжать trace context
   - При обработке: добавлять span на клиенте
3. Инжекция trace context в сообщения (через headers)
4. Экспорт в Jaeger/Zipkin/OTLP

### Шаг 4: Интеграция
**Задачи:**
1. Интеграция с BrokerServer
2. Конфигурация экспортеров

### Шаг 5: Тестирование
**Задачи:**
1. Тесты с Jaeger
2. Проверка трассировки end-to-end

---

## Audit logging

### Шаг 1: Проектирование
**Задачи:**
1. Определить события для логирования:
   - Подключения/отключения клиентов
   - Публикация сообщений
   - Подписки/отписки
   - Создание/удаление очередей
   - Аутентификация (успешная/неуспешная)
   - Административные операции
2. Определить формат логов (структурированное логирование)

### Шаг 2: Реализация Audit Logger
**Файл:** `src/VibeMQ.Core/Interfaces/IAuditLogger.cs`
**Файл:** `src/VibeMQ.Server/Audit/AuditLogger.cs`

**Задачи:**
1. Создать интерфейс `IAuditLogger`
2. Реализовать logger с использованием `ILogger`
3. Интегрировать в ключевые точки:
   - ConnectHandler
   - PublishHandler
   - SubscribeHandler
   - QueueManager операции
4. Логирование в структурированном формате (JSON)

### Шаг 3: Конфигурация
**Задачи:**
1. Добавить опции для включения audit logging
2. Настройка уровня детализации
3. Ротация логов

### Шаг 4: Тестирование
**Задачи:**
1. Тесты логирования событий
2. Проверка формата логов
