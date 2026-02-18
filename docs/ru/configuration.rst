===============
Конфигурация
===============

Это руководство описывает все параметры конфигурации VibeMQ.

.. contents:: Содержание
   :local:
   :depth: 2

Конфигурация сервера
====================

BrokerOptions
-------------

Основной класс конфигурации сервера:

.. code-block:: csharp

   public sealed class BrokerOptions {
       public int Port { get; set; } = 8080;
       public int MaxConnections { get; set; } = 1000;
       public int MaxMessageSize { get; set; } = 1_048_576;
       public bool EnableAuthentication { get; set; }
       public string? AuthToken { get; set; }
       public QueueDefaults QueueDefaults { get; set; } = new();
       public TlsOptions Tls { get; set; } = new();
       public RateLimitOptions RateLimit { get; set; } = new();
   }

Основные параметры
------------------

Port
~~~~

**Тип:** ``int``

**По умолчанию:** ``8080``

TCP порт для подключений клиентов:

.. code-block:: csharp

   .UsePort(8080)

.. note::

   Убедитесь, что порт не занят другими приложениями.

MaxConnections
~~~~~~~~~~~~~~

**Тип:** ``int``

**По умолчанию:** ``1000``

Максимальное количество одновременных подключений:

.. code-block:: csharp

   .UseMaxConnections(5000)

.. note::

   Увеличьте для high-load сценариев.

MaxMessageSize
~~~~~~~~~~~~~~

**Тип:** ``int``

**По умолчанию:** ``1_048_576`` (1 MB)

Максимальный размер сообщения в байтах:

.. code-block:: csharp

   .UseMaxMessageSize(2_097_152)  // 2 MB

.. warning::

   Большие сообщения увеличивают использование памяти.

EnableAuthentication
~~~~~~~~~~~~~~~~~~~~

**Тип:** ``bool``

**По умолчанию:** ``false``

Включить аутентификацию:

.. code-block:: csharp

   .UseAuthentication("my-secret-token")

AuthToken
~~~~~~~~~

**Тип:** ``string?``

**По умолчанию:** ``null``

Токен для аутентификации клиентов:

.. code-block:: csharp

   .UseAuthentication("complex-token-with-32-chars-min")

.. warning::

   Используйте сложные токены (32+ символа) в production.

QueueDefaults
-------------

Настройки очередей по умолчанию:

.. code-block:: csharp

   public sealed class QueueDefaults {
       public DeliveryMode DefaultDeliveryMode { get; set; } = DeliveryMode.RoundRobin;
       public int MaxQueueSize { get; set; } = 10_000;
       public bool EnableAutoCreate { get; set; } = true;
       public TimeSpan? MessageTtl { get; set; }
       public bool EnableDeadLetterQueue { get; set; } = false;
       public string? DeadLetterQueueName { get; set; }
       public OverflowStrategy OverflowStrategy { get; set; } = OverflowStrategy.DropOldest;
       public int MaxRetryAttempts { get; set; } = 3;
   }

DefaultDeliveryMode
~~~~~~~~~~~~~~~~~~~

**Тип:** ``DeliveryMode``

**По умолчанию:** ``RoundRobin``

Режим доставки по умолчанию:

.. code-block:: csharp

   options.DefaultDeliveryMode = DeliveryMode.RoundRobin;

**Возможные значения:**

- ``RoundRobin`` — циклическая доставка одному подписчику
- ``FanOutWithAck`` — всем с подтверждением
- ``FanOutWithoutAck`` — всем без подтверждения
- ``PriorityBased`` — по приоритету

MaxQueueSize
~~~~~~~~~~~~

**Тип:** ``int``

**По умолчанию:** ``10_000``

Максимальное количество сообщений в очереди:

.. code-block:: csharp

   options.MaxQueueSize = 100_000;

EnableAutoCreate
~~~~~~~~~~~~~~~~

**Тип:** ``bool``

**По умолчанию:** ``true``

Автоматическое создание очередей при первой публикации:

.. code-block:: csharp

   options.EnableAutoCreate = true;

.. note::

   Отключите для строгого контроля очередей.

MessageTtl
~~~~~~~~~~

**Тип:** ``TimeSpan?``

**По умолчанию:** ``null`` (без ограничения)

Время жизни сообщений:

.. code-block:: csharp

   options.MessageTtl = TimeSpan.FromHours(24);

.. note::

   Истёкшие сообщения автоматически удаляются.

EnableDeadLetterQueue
~~~~~~~~~~~~~~~~~~~~~

**Тип:** ``bool``

**По умолчанию:** ``false``

Включить Dead Letter Queue:

.. code-block:: csharp

   options.EnableDeadLetterQueue = true;

DeadLetterQueueName
~~~~~~~~~~~~~~~~~~~

**Тип:** ``string?``

**По умолчанию:** ``null`` (автоимя)

Имя Dead Letter Queue:

.. code-block:: csharp

   options.DeadLetterQueueName = "dead-letters";

OverflowStrategy
~~~~~~~~~~~~~~~~

**Тип:** ``OverflowStrategy``

**По умолчанию:** ``DropOldest``

Стратегия переполнения очереди:

.. code-block:: csharp

   options.OverflowStrategy = OverflowStrategy.DropOldest;

**Возможные значения:**

- ``DropOldest`` — удалить старейшее сообщение
- ``DropNewest`` — отклонить новое сообщение
- ``BlockPublisher`` — блокировать отправителя
- ``RedirectToDlq`` — перенаправить в DLQ

MaxRetryAttempts
~~~~~~~~~~~~~~~~

**Тип:** ``int``

**По умолчанию:** ``3``

Максимальное количество попыток доставки:

.. code-block:: csharp

   options.MaxRetryAttempts = 5;

TlsOptions
----------

Настройки TLS/SSL:

.. code-block:: csharp

   public sealed class TlsOptions {
       public bool Enabled { get; set; }
       public string? CertificatePath { get; set; }
       public string? CertificatePassword { get; set; }
   }

Enabled
~~~~~~~

**Тип:** ``bool``

**По умолчанию:** ``false``

Включить TLS:

.. code-block:: csharp

   .UseTls(options => {
       options.Enabled = true;
       options.CertificatePath = "/path/to/cert.pfx";
       options.CertificatePassword = "cert-password";
   })

CertificatePath
~~~~~~~~~~~~~~~

**Тип:** ``string?``

**По умолчанию:** ``null``

Путь к PFX-сертификату:

.. code-block:: csharp

   options.CertificatePath = "/etc/ssl/vibemq.pfx";

CertificatePassword
~~~~~~~~~~~~~~~~~~~

**Тип:** ``string?``

**По умолчанию:** ``null``

Пароль к сертификату:

.. code-block:: csharp

   options.CertificatePassword = "secure-password";

RateLimitOptions
----------------

Настройки rate limiting:

.. code-block:: csharp

   public sealed class RateLimitOptions {
       public bool Enabled { get; set; }
       public int MaxConnectionsPerIpPerWindow { get; set; } = 100;
       public int ConnectionWindowSeconds { get; set; } = 60;
       public int MaxMessagesPerClientPerSecond { get; set; } = 1000;
   }

Enabled
~~~~~~~

**Тип:** ``bool``

**По умолчанию:** ``false``

Включить rate limiting:

.. code-block:: csharp

   .ConfigureRateLimiting(options => {
       options.Enabled = true;
   })

MaxConnectionsPerIpPerWindow
~~~~~~~~~~~~~~~~~~~~~~~~~~~~

**Тип:** ``int``

**По умолчанию:** ``100``

Максимальное количество подключений с одного IP в окно времени:

.. code-block:: csharp

   options.MaxConnectionsPerIpPerWindow = 50;

ConnectionWindowSeconds
~~~~~~~~~~~~~~~~~~~~~~~

**Тип:** ``int``

**По умолчанию:** ``60``

Окно времени для ограничения подключений (секунды):

.. code-block:: csharp

   options.ConnectionWindowSeconds = 120;

MaxMessagesPerClientPerSecond
~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

**Тип:** ``int``

**По умолчанию:** ``1000``

Максимальное количество сообщений от клиента в секунду:

.. code-block:: csharp

   options.MaxMessagesPerClientPerSecond = 500;

Конфигурация клиента
====================

ClientOptions
-------------

.. code-block:: csharp

   public sealed class ClientOptions {
       public string? AuthToken { get; set; }
       public ReconnectPolicy ReconnectPolicy { get; set; } = new();
       public TimeSpan KeepAliveInterval { get; set; } = TimeSpan.FromSeconds(30);
       public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromSeconds(10);
       public bool UseTls { get; set; }
       public bool SkipCertificateValidation { get; set; }
   }

AuthToken
~~~~~~~~~

**Тип:** ``string?``

**По умолчанию:** ``null``

Токен для аутентификации:

.. code-block:: csharp

   AuthToken = "my-secret-token"

ReconnectPolicy
~~~~~~~~~~~~~~~

**Тип:** ``ReconnectPolicy``

Политика переподключения:

.. code-block:: csharp

   ReconnectPolicy = new ReconnectPolicy {
       MaxAttempts = 10,
       InitialDelay = TimeSpan.FromSeconds(1),
       MaxDelay = TimeSpan.FromMinutes(5),
       UseExponentialBackoff = true
   }

KeepAliveInterval
~~~~~~~~~~~~~~~~~

**Тип:** ``TimeSpan``

**По умолчанию:** ``30 секунд``

Интервал отправки PING:

.. code-block:: csharp

   KeepAliveInterval = TimeSpan.FromSeconds(60)

CommandTimeout
~~~~~~~~~~~~~~

**Тип:** ``TimeSpan``

**По умолчанию:** ``10 секунд``

Таймаут для команд:

.. code-block:: csharp

   CommandTimeout = TimeSpan.FromSeconds(30)

UseTls
~~~~~~

**Тип:** ``bool``

**По умолчанию:** ``false``

Использовать TLS:

.. code-block:: csharp

   UseTls = true

SkipCertificateValidation
~~~~~~~~~~~~~~~~~~~~~~~~~

**Тип:** ``bool``

**По умолчанию:** ``false``

Пропускать валидацию сертификата:

.. code-block:: csharp

   SkipCertificateValidation = true  // Только для тестов!

.. warning::

   Не используйте в production!

ReconnectPolicy
---------------

.. code-block:: csharp

   public sealed class ReconnectPolicy {
       public int MaxAttempts { get; set; } = int.MaxValue;
       public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);
       public TimeSpan MaxDelay { get; set; } = TimeSpan.FromMinutes(5);
       public bool UseExponentialBackoff { get; set; } = true;
   }

MaxAttempts
~~~~~~~~~~~

**Тип:** ``int``

**По умолчанию:** ``int.MaxValue``

Максимальное количество попыток:

.. code-block:: csharp

   MaxAttempts = 50

InitialDelay
~~~~~~~~~~~~

**Тип:** ``TimeSpan``

**По умолчанию:** ``1 секунда``

Начальная задержка между попытками:

.. code-block:: csharp

   InitialDelay = TimeSpan.FromSeconds(2)

MaxDelay
~~~~~~~~

**Тип:** ``TimeSpan``

**По умолчанию:** ``5 минут``

Максимальная задержка:

.. code-block:: csharp

   MaxDelay = TimeSpan.FromMinutes(1)

UseExponentialBackoff
~~~~~~~~~~~~~~~~~~~~~

**Тип:** ``bool``

**По умолчанию:** ``true``

Использовать экспоненциальное увеличение задержки:

.. code-block:: csharp

   UseExponentialBackoff = true

Health check конфигурация
=========================

HealthCheckOptions
------------------

.. code-block:: csharp

   public sealed class HealthCheckOptions {
       public bool Enabled { get; set; } = true;
       public int Port { get; set; } = 8081;
   }

Enabled
~~~~~~~

**Тип:** ``bool``

**По умолчанию:** ``true``

Включить health check сервер:

.. code-block:: csharp

   .ConfigureHealthChecks(options => {
       options.Enabled = true;
   })

Port
~~~~

**Тип:** ``int``

**По умолчанию:** ``8081``

HTTP порт для health checks:

.. code-block:: csharp

   .ConfigureHealthChecks(options => {
       options.Port = 9090;
   })

Примеры конфигурации
====================

Минимальная
-----------

.. code-block:: csharp

   var broker = BrokerBuilder.Create()
       .UsePort(8080)
       .Build();

Разработка
----------

.. code-block:: csharp

   var broker = BrokerBuilder.Create()
       .UsePort(8080)
       .UseAuthentication("dev-token")
       .ConfigureQueues(options => {
           options.DefaultDeliveryMode = DeliveryMode.RoundRobin;
           options.MaxQueueSize = 10_000;
           options.EnableAutoCreate = true;
       })
       .Build();

Production
----------

.. code-block:: csharp

   var broker = BrokerBuilder.Create()
       .UsePort(8080)
       .UseAuthentication(Environment.GetEnvironmentVariable("VIBEMQ_TOKEN"))
       .UseMaxConnections(5000)
       .UseMaxMessageSize(2_097_152)
       .ConfigureQueues(options => {
           options.DefaultDeliveryMode = DeliveryMode.FanOutWithAck;
           options.MaxQueueSize = 100_000;
           options.EnableDeadLetterQueue = true;
           options.MaxRetryAttempts = 5;
           options.MessageTtl = TimeSpan.FromDays(1);
       })
       .ConfigureRateLimiting(options => {
           options.Enabled = true;
           options.MaxConnectionsPerIpPerWindow = 100;
           options.MaxMessagesPerClientPerSecond = 5000;
       })
       .UseTls(options => {
           options.Enabled = true;
           options.CertificatePath = "/etc/ssl/vibemq.pfx";
           options.CertificatePassword = Environment.GetEnvironmentVariable("CERT_PASSWORD");
       })
       .ConfigureHealthChecks(options => {
           options.Enabled = true;
           options.Port = 8081;
       })
       .Build();

IoT сценарий
------------

.. code-block:: csharp

   var broker = BrokerBuilder.Create()
       .UsePort(8883)
       .UseAuthentication("iot-token")
       .UseMaxConnections(10000)
       .ConfigureQueues(options => {
           options.DefaultDeliveryMode = DeliveryMode.RoundRobin;
           options.MaxQueueSize = 1000;
           options.MessageTtl = TimeSpan.FromSeconds(60);
       })
       .ConfigureRateLimiting(options => {
           options.Enabled = true;
           options.MaxConnectionsPerIpPerWindow = 500;
           options.MaxMessagesPerClientPerSecond = 100;
       })
       .Build();

Микросервисы
------------

.. code-block:: csharp

   var broker = BrokerBuilder.Create()
       .UsePort(8080)
       .UseAuthentication("microservice-token")
       .ConfigureQueues(options => {
           options.DefaultDeliveryMode = DeliveryMode.FanOutWithAck;
           options.EnableAutoCreate = true;
           options.MessageTtl = TimeSpan.FromMinutes(30);
           options.EnableDeadLetterQueue = true;
       })
       .ConfigureHealthChecks(options => {
           options.Enabled = true;
           options.Port = 8081;
       })
       .Build();

Следующие шаги
==============

- :doc:`server-setup` — настройка сервера
- :doc:`di-integration` — интеграция с DI
- :doc:`monitoring` — мониторинг
