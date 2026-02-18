======================================
VibeMQ — Месседж-брокер на .NET
======================================

.. image:: https://img.shields.io/badge/.NET-8.0+-blue.svg
   :alt: .NET Version
   :target: https://dotnet.microsoft.com/

.. image:: https://img.shields.io/badge/license-MIT-green.svg
   :alt: License
   :target: https://opensource.org/licenses/MIT

**VibeMQ** — это простой, но надёжный месседж-брокер для .NET приложений с использованием TCP в качестве транспорта. Он поддерживает публикацию/подписку (pub/sub), очереди с гарантией доставки, автоматические реконнекты и другие важные функции для построения распределённых систем.

.. contents:: Содержание
   :local:
   :depth: 2

🚀 Быстрый старт
===============

Установка через NuGet:

.. code-block:: bash

   dotnet add package VibeMQ.Server
   dotnet add package VibeMQ.Client

Запуск сервера:

.. code-block:: csharp

   using VibeMQ.Server;
   using VibeMQ.Core.Enums;

   var broker = BrokerBuilder.Create()
       .UsePort(8080)
       .UseAuthentication("my-secret-token")
       .ConfigureQueues(options => {
           options.DefaultDeliveryMode = DeliveryMode.RoundRobin;
           options.MaxQueueSize = 10_000;
       })
       .Build();

   await broker.RunAsync(cancellationToken);

Подключение клиента:

.. code-block:: csharp

   using VibeMQ.Client;

   await using var client = await VibeMQClient.ConnectAsync(
       "localhost", 
       8080, 
       new ClientOptions { AuthToken = "my-secret-token" }
   );

   // Публикация сообщения
   await client.PublishAsync("notifications", new { Title = "Hello", Body = "World" });

   // Подписка на сообщения
   await using var subscription = await client.SubscribeAsync<dynamic>(
       "notifications",
       msg => {
           Console.WriteLine($"Получено: {msg.Title}");
           return Task.CompletedTask;
       }
   );

📋 Оглавление
=============

.. toctree::
   :maxdepth: 2
   :caption: Начало работы

   getting-started
   installation

.. toctree::
   :maxdepth: 2
   :caption: Основы

   architecture
   features
   protocol

.. toctree::
   :maxdepth: 2
   :caption: Настройка и использование

   server-setup
   client-usage
   configuration
   di-integration

.. toctree::
   :maxdepth: 2
   :caption: Мониторинг и обслуживание

   monitoring
   health-checks
   troubleshooting

.. toctree::
   :maxdepth: 2
   :caption: Дополнительно

   examples
   faq
   changelog

🎯 Ключевые возможности
======================

**Режимы доставки сообщений:**

- **Round-robin** — каждое сообщение доставляется одному подписчику (циклически)
- **Fan-out с подтверждением** — всем подписчикам с гарантией доставки
- **Fan-out без подтверждения** — всем подписчикам без подтверждения
- **Priority-based** — доставка по приоритету (Critical > High > Normal > Low)

**Гарантии доставки:**

- Подтверждения (ACK) от получателей
- Автоматические повторные попытки с экспоненциальной задержкой
- Dead Letter Queue для неудачных сообщений
- Отслеживание неподтверждённых сообщений

**Надёжность:**

- Keep-alive (PING/PONG) для поддержания соединений
- Автоматические реконнекты на стороне клиента
- Graceful shutdown без потери сообщений
- Health checks для оркестраторов

**Безопасность:**

- Токен-базированная аутентификация
- Поддержка TLS/SSL шифрования
- Rate limiting для защиты от перегрузок

**Мониторинг:**

- Встроенные метрики производительности
- HTTP эндпоинты для health checks
- Статистика по очередям и подключениям

📦 Модульная архитектура
=======================

VibeMQ состоит из нескольких NuGet-пакетов:

+--------------------------------+------------------------------------------+
| Пакет                          | Описание                                 |
+================================+==========================================+
| ``VibeMQ.Server``              | Сервер брокера                           |
+--------------------------------+------------------------------------------+
| ``VibeMQ.Client``              | Клиент для подключения к брокеру         |
+--------------------------------+------------------------------------------+
| ``VibeMQ.Core``                | Ядро: модели, интерфейсы, конфигурация   |
+--------------------------------+------------------------------------------+
| ``VibeMQ.Protocol``            | Протокол обмена сообщениями              |
+--------------------------------+------------------------------------------+
| ``VibeMQ.Health``              | HTTP health check сервер                 |
+--------------------------------+------------------------------------------+
| ``VibeMQ.Server.DependencyInjection``    | Интеграция сервера с DI      |
+--------------------------------+------------------------------------------+
| ``VibeMQ.Client.DependencyInjection``    | Интеграция клиента с DI       |
+--------------------------------+------------------------------------------+

💡 Примеры использования
========================

Сервер с Dependency Injection:

.. code-block:: csharp

   using VibeMQ.Server.DependencyInjection;
   using VibeMQ.Core.Enums;

   var host = Host.CreateDefaultBuilder(args)
       .ConfigureServices(services => {
           services.AddVibeMQBroker(options => {
               options.Port = 8080;
               options.EnableAuthentication = true;
               options.AuthToken = "my-secret-token";
               options.QueueDefaults.DefaultDeliveryMode = DeliveryMode.RoundRobin;
           });
       })
       .Build();

   await host.RunAsync();

Клиент с Dependency Injection:

.. code-block:: csharp

   using VibeMQ.Client.DependencyInjection;

   var host = Host.CreateDefaultBuilder(args)
       .ConfigureServices(services => {
           services.AddVibeMQClient(settings => {
               settings.Host = "localhost";
               settings.Port = 8080;
               settings.ClientOptions.AuthToken = "my-secret-token";
           });
       })
       .Build();

   var factory = host.Services.GetRequiredService<IVibeMQClientFactory>();
   await using var client = await factory.CreateAsync();

🔗 Ссылки
=========

- `GitHub репозиторий <https://github.com/DarkBoy/VibeMQ>`_
- `NuGet пакеты <https://www.nuget.org/packages?q=VibeMQ>`_
- `Issues и предложения <https://github.com/DarkBoy/VibeMQ/issues>`_

📄 Лицензия
===========

VibeMQ распространяется под лицензией MIT. Подробности см. в файле LICENSE.
