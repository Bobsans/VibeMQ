==========================
Часто задаваемые вопросы
==========================

Это руководство отвечает на распространённые вопросы о VibeMQ.

.. contents:: Содержание
   :local:
   :depth: 2

Общие вопросы
=============

Что такое VibeMQ?
-----------------

**VibeMQ** — это простой, но надёжный месседж-брокер для .NET приложений. Он поддерживает:

- Публикацию/подписку (pub/sub)
- Очереди с гарантией доставки
- Автоматические реконнекты
- Аутентификацию по токену
- TLS/SSL шифрование
- Health checks для оркестраторов

Чем VibeMQ отличается от RabbitMQ/Kafka?
-----------------------------------------

**VibeMQ vs RabbitMQ:**

- VibeMQ проще в настройке и использовании
- Не требует внешних зависимостей (баз данных и т.д.)
- Написан на C# для .NET экосистемы
- Меньше функциональности, но достаточно для большинства сценариев

**VibeMQ vs Kafka:**

- VibeMQ не является распределённым логом событий
- Не поддерживает персистентное хранение (в текущей версии)
- Проще в развёртывании и использовании
- Лучше подходит для task queues и real-time уведомлений

Когда использовать VibeMQ?
--------------------------

**Хорошие сценарии:**

- Task queues для фоновой обработки
- Уведомления между микросервисами
- Real-time обновления
- Событийная архитектура
- Распределённые системы на .NET

**Не подходит для:**

- Хранения больших объёмов данных
- Стриминга в реальном времени (используйте Kafka)
- Сложной маршрутизации сообщений (используйте RabbitMQ)
- Транзакционных сообщений

Какие версии .NET поддерживаются?
---------------------------------

- **.NET 8.0** (основная целевая платформа)
- **.NET 10.0** (планируется поддержка)

Технические вопросы
===================

Как работает гарантия доставки?
-------------------------------

VibeMQ использует механизм подтверждений (ACK):

1. Брокер отправляет сообщение подписчику
2. Запускается таймер ожидания ACK
3. Подписчик обрабатывает сообщение и отправляет ACK
4. Если ACK не получен — повторная отправка
5. После исчерпания попыток — Dead Letter Queue

Что происходит при перезапуске сервера?
---------------------------------------

**Текущая версия:**

- Сообщения в очередях теряются (in-memory хранение)
- Подключения необходимо восстанавливать

**Планируется:**

- Persistence слой для сохранения сообщений
- Восстановление состояния после перезапуска

Как масштабировать VibeMQ?
--------------------------

**Горизонтальное масштабирование:**

1. Запустите несколько экземпляров сервера
2. Используйте балансировщик нагрузки
3. Клиенты подключаются к ближайшему серверу

**Вертикальное масштабирование:**

- Увеличьте лимиты (MaxConnections, MaxQueueSize)
- Добавьте ресурсов (CPU, RAM)

**Планируется:**

- Кластеризация для автоматического масштабирования

Как обеспечить безопасность?
----------------------------

**Аутентификация:**

.. code-block:: csharp

   .UseAuthentication("my-secret-token")

**TLS шифрование:**

.. code-block:: csharp

   .UseTls(options => {
       options.Enabled = true;
       options.CertificatePath = "/path/to/cert.pfx";
   })

**Rate limiting:**

.. code-block:: csharp

   .ConfigureRateLimiting(options => {
       options.Enabled = true;
       options.MaxConnectionsPerIpPerWindow = 100;
   })

Какие режимы доставки поддерживаются?
-------------------------------------

- **Round-robin** — каждому подписчику по очереди
- **Fan-out с ACK** — всем с подтверждением
- **Fan-out без ACK** — всем без подтверждения
- **Priority-based** — по приоритету

Как работает Dead Letter Queue?
-------------------------------

DLQ хранит сообщения, которые не удалось доставить:

.. code-block:: csharp

   .ConfigureQueues(options => {
       options.EnableDeadLetterQueue = true;
       options.MaxRetryAttempts = 3;
   });

**Причины попадания в DLQ:**

- Превышено количество попыток доставки
- Истёк TTL сообщения
- Ошибка десериализации
- Исключение в обработчике

Развёртывание
=============

Как запустить в Docker?
-----------------------

**Dockerfile:**

.. code-block:: dockerfile

   FROM mcr.microsoft.com/dotnet/runtime:8.0
   WORKDIR /app
   EXPOSE 8080 8081
   COPY . .
   ENTRYPOINT ["dotnet", "VibeMQ.Server.dll"]

**Запуск:**

.. code-block:: bash

   docker build -t vibemq-server .
   docker run -p 8080:8080 -p 8081:8081 vibemq-server

Как использовать с Kubernetes?
------------------------------

**Deployment:**

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
       spec:
         containers:
         - name: vibemq
           image: vibemq-server:latest
           ports:
           - containerPort: 8080
           - containerPort: 8081
           livenessProbe:
             httpGet:
               path: /health/
               port: 8081
             initialDelaySeconds: 10
             periodSeconds: 10

Как настроить для production?
-----------------------------

**Рекомендации:**

.. code-block:: csharp

   var broker = BrokerBuilder.Create()
       .UsePort(8080)
       .UseAuthentication(Environment.GetEnvironmentVariable("VIBEMQ_TOKEN"))
       .UseMaxConnections(5000)
       .ConfigureQueues(options => {
           options.DefaultDeliveryMode = DeliveryMode.FanOutWithAck;
           options.MaxQueueSize = 100_000;
           options.EnableDeadLetterQueue = true;
           options.MaxRetryAttempts = 5;
       })
       .ConfigureRateLimiting(options => {
           options.Enabled = true;
           options.MaxConnectionsPerIpPerWindow = 100;
           options.MaxMessagesPerClientPerSecond = 5000;
       })
       .UseTls(options => {
           options.Enabled = true;
           options.CertificatePath = "/etc/ssl/vibemq.pfx";
       })
       .ConfigureHealthChecks(options => {
           options.Enabled = true;
           options.Port = 8081;
       })
       .Build();

Производительность
==================

Какая производительность VibeMQ?
--------------------------------

**Бенчмарки:**

- 10,000+ сообщений/сек на одном узле
- Латентность < 10ms для 95% сообщений
- Поддержка 1000+ одновременных подключений

**Факторы, влияющие на производительность:**

- Размер сообщений
- Количество подписчиков
- Режим доставки
- Сетевая задержка

Как оптимизировать производительность?
--------------------------------------

**На сервере:**

.. code-block:: csharp

   .UseMaxConnections(5000)  // Увеличьте лимит
   .ConfigureQueues(options => {
       options.MaxQueueSize = 100_000;  // Больше очередь
       options.OverflowStrategy = OverflowStrategy.DropOldest;  // Быстрая стратегия
   })

**На клиенте:**

.. code-block:: csharp

   new ClientOptions {
       KeepAliveInterval = TimeSpan.FromSeconds(60),  // Реже PING
       CommandTimeout = TimeSpan.FromSeconds(5)  // Меньше таймаут
   }

**Общие рекомендации:**

- Используйте батчинг для публикации
- Оптимизируйте обработчики сообщений
- Мониторьте метрики производительности

Безопасность
============

Насколько безопасна аутентификация по токену?
---------------------------------------------

**Рекомендации:**

- Используйте сложные токены (32+ символа)
- Храните токены в защищённом месте (Key Vault, Secrets Manager)
- Меняйте токены периодически
- Используйте разные токены для разных сред

.. warning::

   Токен-аутентификация подходит для внутренней безопасности.
   Для публичных API используйте OAuth2/OIDC.

Нужно ли использовать TLS?
--------------------------

**Да, если:**

- Сообщения содержат чувствительные данные
- Сеть не доверенная
- Требуется соответствие стандартам безопасности

**Нет, если:**

- Все сервисы в доверенной сети
- TLS terminates на уровне load balancer

Как защитить от DDoS?
---------------------

**Rate limiting:**

.. code-block:: csharp

   .ConfigureRateLimiting(options => {
       options.Enabled = true;
       options.MaxConnectionsPerIpPerWindow = 50;
       options.MaxMessagesPerClientPerSecond = 100;
   })

**Дополнительно:**

- Используйте firewall
- Ограничьте количество подключений
- Мониторьте аномалии

Интеграция
==========

Как использовать с ASP.NET Core?
--------------------------------

.. code-block:: csharp

   using VibeMQ.Server.DependencyInjection;
   using VibeMQ.Client.DependencyInjection;

   var builder = WebApplication.CreateBuilder(args);

   // Сервер
   builder.Services.AddVibeMQBroker(options => {
       options.Port = 8080;
   });

   // Клиент
   builder.Services.AddVibeMQClient(settings => {
       settings.Host = "localhost";
       settings.Port = 8080;
   });

   var app = builder.Build();
   await app.RunAsync();

Как использовать с Worker Service?
----------------------------------

.. code-block:: csharp

   using VibeMQ.Client.DependencyInjection;

   var host = Host.CreateDefaultBuilder(args)
       .ConfigureServices(services => {
           services.AddHostedService<Worker>();
           services.AddVibeMQClient(settings => {
               settings.Host = "localhost";
               settings.Port = 8080;
           });
       })
       .Build();

   await host.RunAsync();

Можно ли использовать с другими языками?
----------------------------------------

**Текущая версия:** Только .NET клиенты

**Планируется:**

- Клиенты для Java, Python, Node.js
- Поддержка AMQP/MQTT протоколов

Устранение проблем
==================

Почему сообщения теряются?
--------------------------

**Причины:**

- Сервер перезапустился (in-memory хранение)
- Истёк TTL сообщений
- Превышен размер очереди

**Решение:**

.. code-block:: csharp

   .ConfigureQueues(options => {
       options.EnableDeadLetterQueue = true;  // Сохранение неудачных
       options.MessageTtl = TimeSpan.FromHours(24);  // Увеличьте TTL
       options.MaxQueueSize = 100_000;  // Увеличьте размер
   })

Почему высокие задержки?
------------------------

**Причины:**

- Перегрузка сервера
- Медленные обработчики
- Сетевые проблемы

**Решение:**

- Оптимизируйте обработчики
- Увеличьте ресурсы сервера
- Проверьте сеть

Почему частые отключения?
-------------------------

**Причины:**

- Проблемы с сетью
- Таймаут keep-alive
- Сервер перезапускается

**Решение:**

.. code-block:: csharp

   new ClientOptions {
       ReconnectPolicy = new ReconnectPolicy {
           MaxAttempts = 50,  // Увеличьте попытки
           UseExponentialBackoff = true
       },
       KeepAliveInterval = TimeSpan.FromSeconds(30)  // Проверьте keep-alive
   }

Следующие шаги
==============

- :doc:`getting-started` — быстрое начало
- :doc:`troubleshooting` — устранение проблем
- :doc:`examples` — примеры использования
