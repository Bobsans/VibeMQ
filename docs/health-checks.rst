=============
Health Checks
=============

Это руководство описывает использование health checks для мониторинга и оркестрации.

.. contents:: Содержание
   :local:
   :depth: 2

Обзор
=====

VibeMQ предоставляет HTTP эндпоинты для проверки здоровья сервера. Это используется оркестраторами (Kubernetes, Docker Swarm) для определения состояния сервиса.

Включение health checks
=======================

.. code-block:: csharp

   using VibeMQ.Health;

   var broker = BrokerBuilder.Create()
       .UsePort(8080)
       .ConfigureHealthChecks(options => {
           options.Enabled = true;
           options.Port = 8081;
       })
       .Build();

Параметры конфигурации
----------------------

+------------------------+------------------+----------------------------------+
| Параметр               | По умолчанию     | Описание                         |
+========================+==================+==================================+
| ``Enabled``            | true             | Включить health check сервер     |
+------------------------+------------------+----------------------------------+
| ``Port``               | 8081             | HTTP порт для health checks      |
+------------------------+------------------+----------------------------------+

Эндпоинты
=========

GET /health/
============

Проверка здоровья сервера.

**Запрос:**

.. code-block:: bash

   curl http://localhost:8081/health/

**Ответ (здоров):**

.. code-block:: json

   {
     "status": "healthy",
     "active_connections": 15,
     "queue_count": 5,
     "memory_usage_mb": 256
   }

**Код состояния:** ``200 OK``

**Ответ (нездоров):**

.. code-block:: json

   {
     "status": "unhealthy",
     "active_connections": 0,
     "queue_count": 0,
     "memory_usage_mb": 512
   }

**Код состояния:** ``503 Service Unavailable``

**Критерии здоровья:**

- ``healthy`` — использование памяти < 90%
- ``unhealthy`` — использование памяти >= 90%

GET /metrics/
=============

Получение подробных метрик.

**Запрос:**

.. code-block:: bash

   curl http://localhost:8081/metrics/

**Ответ:**

.. code-block:: json

   {
     "total_messages_published": 125000,
     "total_messages_delivered": 124850,
     "total_messages_acknowledged": 124800,
     "total_retries": 150,
     "total_dead_lettered": 50,
     "total_errors": 5,
     "total_connections_accepted": 500,
     "total_connections_rejected": 10,
     "active_connections": 15,
     "active_queues": 5,
     "in_flight_messages": 42,
     "memory_usage_bytes": 268435456,
     "average_delivery_latency_ms": 2.5,
     "timestamp": "2026-02-18T10:30:00Z"
   }

Использование с оркестраторами
==============================

Kubernetes
----------

**Deployment с health checks:**

.. code-block:: yaml

   apiVersion: apps/v1
   kind: Deployment
   metadata:
     name: vibemq
   spec:
     replicas: 3
     selector:
       matchLabels:
         app: vibemq
     template:
       metadata:
         labels:
           app: vibemq
       spec:
         containers:
         - name: vibemq
           image: vibemq-server:latest
           ports:
           - containerPort: 8080
             name: tcp
           - containerPort: 8081
             name: http
           livenessProbe:
             httpGet:
               path: /health/
               port: 8081
             initialDelaySeconds: 10
             periodSeconds: 10
             timeoutSeconds: 5
             failureThreshold: 3
           readinessProbe:
             httpGet:
               path: /health/
               port: 8081
             initialDelaySeconds: 5
             periodSeconds: 5
             timeoutSeconds: 3
             failureThreshold: 3
           resources:
             requests:
               memory: "512Mi"
               cpu: "250m"
             limits:
               memory: "1Gi"
               cpu: "500m"

**Описание probes:**

- **livenessProbe** — определяет, нужно ли перезапускать контейнер
- **readinessProbe** — определяет, готов ли контейнер принимать трафик

**Параметры:**

- ``initialDelaySeconds`` — задержка перед первой проверкой
- ``periodSeconds`` — интервал между проверками
- ``timeoutSeconds`` — таймаут проверки
- ``failureThreshold`` — количество неудачных попыток

Docker Compose
--------------

.. code-block:: yaml

   version: '3.8'

   services:
     vibemq:
       image: vibemq-server:latest
       ports:
         - "8080:8080"
         - "8081:8081"
       environment:
         - VIBEMQ__PORT=8080
         - VIBEMQ__AUTHTOKEN=my-secret-token
       healthcheck:
         test: ["CMD", "curl", "-f", "http://localhost:8081/health/"]
         interval: 10s
         timeout: 5s
         retries: 3
         start_period: 10s
       restart: unless-stopped

Azure Container Instances
-------------------------

.. code-block:: yaml

   apiVersion: 2019-12-01
   kind: ContainerGroup
   metadata:
     name: vibemq
   spec:
     containers:
     - name: vibemq
       image: vibemq-server:latest
       ports:
       - port: 8080
         protocol: TCP
       - port: 8081
         protocol: TCP
       livenessProbe:
         httpGet:
           path: /health/
           port: 8081
         initialDelaySeconds: 10
         periodSeconds: 10
       readinessProbe:
         httpGet:
           path: /health/
           port: 8081
         initialDelaySeconds: 5
         periodSeconds: 5

AWS ECS
-------

**Task definition:**

.. code-block:: json

   {
     "family": "vibemq",
     "containerDefinitions": [
       {
         "name": "vibemq",
         "image": "vibemq-server:latest",
         "portMappings": [
           {
             "containerPort": 8080,
             "hostPort": 8080,
             "protocol": "tcp"
           },
           {
             "containerPort": 8081,
             "hostPort": 8081,
             "protocol": "tcp"
           }
         ],
         "healthCheck": {
           "command": [
             "CMD-SHELL",
             "curl -f http://localhost:8081/health/ || exit 1"
           ],
           "interval": 10,
           "timeout": 5,
           "retries": 3,
           "startPeriod": 10
         }
       }
     ]
   }

Мониторинг health checks
========================

Проверка вручную
----------------

.. code-block:: bash

   # Проверка здоровья
   curl http://localhost:8081/health/

   # Подробные метрики
   curl http://localhost:8081/metrics/

   # С форматированием
   curl -s http://localhost:8081/metrics/ | jq

PowerShell:

.. code-block:: powershell

   # Проверка здоровья
   Invoke-RestMethod -Uri http://localhost:8081/health/

   # Проверка с обработкой ошибок
   try {
       $response = Invoke-RestMethod -Uri http://localhost:8081/health/
       if ($response.status -eq "healthy") {
           Write-Host "✓ Сервер здоров" -ForegroundColor Green
       } else {
           Write-Host "✗ Сервер нездоров" -ForegroundColor Red
       }
   } catch {
       Write-Host "✗ Ошибка подключения: $_" -ForegroundColor Red
   }

Автоматический мониторинг
-------------------------

**Bash скрипт:**

.. code-block:: bash

   #!/bin/bash

   HEALTH_URL="http://localhost:8081/health/"
   METRICS_URL="http://localhost:8081/metrics/"

   # Проверка здоровья
   response=$(curl -s -w "\n%{http_code}" $HEALTH_URL)
   body=$(echo "$response" | head -n -1)
   status_code=$(echo "$response" | tail -n 1)

   if [ "$status_code" -eq 200 ]; then
       echo "✓ VibeMQ здоров (HTTP $status_code)"
       echo "$body" | jq .
   elif [ "$status_code" -eq 503 ]; then
       echo "✗ VibeMQ нездоров (HTTP $status_code)"
       echo "$body" | jq .
       exit 1
   else
       echo "✗ Ошибка подключения (HTTP $status_code)"
       exit 1
   fi

   # Проверка метрик
   metrics=$(curl -s $METRICS_URL)
   echo "Метрики:"
   echo "$metrics" | jq .

**Python скрипт:**

.. code-block:: python

   import requests
   import sys
   import time

   HEALTH_URL = "http://localhost:8081/health/"
   METRICS_URL = "http://localhost:8081/metrics/"

   def check_health():
       try:
           response = requests.get(HEALTH_URL, timeout=5)
           
           if response.status_code == 200:
               data = response.json()
               print(f"✓ VibeMQ здоров")
               print(f"  Статус: {data['status']}")
               print(f"  Подключений: {data['active_connections']}")
               print(f"  Очередей: {data['queue_count']}")
               print(f"  Память: {data['memory_usage_mb']} MB")
               return True
           elif response.status_code == 503:
               print(f"✗ VibeMQ нездоров")
               return False
       except requests.exceptions.RequestException as e:
           print(f"✗ Ошибка подключения: {e}")
           return False
       
       return False

   def get_metrics():
       try:
           response = requests.get(METRICS_URL, timeout=5)
           if response.status_code == 200:
               data = response.json()
               print("\nМетрики:")
               print(f"  Опубликовано: {data['total_messages_published']}")
               print(f"  Доставлено: {data['total_messages_delivered']}")
               print(f"  Подтверждено: {data['total_messages_acknowledged']}")
               print(f"  Ошибок: {data['total_errors']}")
               print(f"  Латентность: {data['average_delivery_latency_ms']:.2f} ms")
       except Exception as e:
           print(f"  Ошибка получения метрик: {e}")

   if __name__ == "__main__":
       if check_health():
           get_metrics()
           sys.exit(0)
       else:
           sys.exit(1)

Интеграция с системами мониторинга
==================================

Prometheus
----------

**prometheus.yml:**

.. code-block:: yaml

   scrape_configs:
     - job_name: 'vibemq'
       static_configs:
         - targets: ['vibemq:8081']
       metrics_path: '/metrics/'
       scrape_interval: 15s
       scrape_timeout: 10s

Grafana
-------

Импортируйте дашборд для визуализации метрик VibeMQ.

**Основные панели:**

- Статус здоровья (health check)
- Активные подключения
- Количество очередей
- Использование памяти
- Throughput сообщений
- Латентность доставки

Datadog
-------

**Конфигурация agent:**

.. code-block:: yaml

   instances:
     - vibemq_url: http://vibemq:8081/metrics/
       tags:
         - "service:vibemq"
         - "env:production"

New Relic
---------

Используйте Prometheus endpoint для интеграции:

.. code-block:: yaml

   integrations:
     - name: prometheus
       metric_types:
         - vibemq_messages_published_total
         - vibemq_messages_delivered_total
         - vibemq_active_connections
       urls:
         - http://vibemq:8081/metrics/

Устранение проблем
==================

Health check не отвечает
------------------------

**Проблема:** ``curl: (7) Failed to connect to localhost port 8081``

**Причины:**

- Health check отключён
- Неправильный порт
- Брандмауэр блокирует

**Решение:**

.. code-block:: csharp

   .ConfigureHealthChecks(options => {
       options.Enabled = true;
       options.Port = 8081;  // Проверьте порт
   })

Возвращается 503
----------------

**Проблема:** Health check возвращает ``503 Service Unavailable``

**Причина:** Критическое использование памяти (>90%)

**Решение:**

1. Увеличьте лимит памяти
2. Уменьшите размер очередей
3. Оптимизируйте использование памяти

.. code-block:: csharp

   .ConfigureQueues(options => {
       options.MaxQueueSize = 5000;  // Уменьшите размер
   })

Таймаут health check
--------------------

**Проблема:** ``curl: (28) Operation timed out``

**Причины:**

- Сервер перегружен
- Неправильный таймаут

**Решение:** Увеличьте таймаут в конфигурации оркестратора:

.. code-block:: yaml

   livenessProbe:
     timeoutSeconds: 10  # Увеличьте таймаут

Следующие шаги
==============

- :doc:`monitoring` — мониторинг и метрики
- :doc:`troubleshooting` — устранение проблем
- :doc:`configuration` — конфигурирование
