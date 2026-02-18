===============
Быстрый старт
===============

Это руководство поможет вам быстро начать работу с VibeMQ — от установки до первого сообщения.

.. contents:: Содержание
   :local:
   :depth: 2

Предварительные требования
==========================

- **.NET 8.0 SDK** или выше
- Базовые знания C# и асинхронного программирования

Установка
=========

Создайте новый проект или откройте существующий:

.. code-block:: bash

   dotnet new console -n VibeMQ.Demo
   cd VibeMQ.Demo

Установите необходимые NuGet-пакеты:

.. code-block:: bash

   dotnet add package VibeMQ.Server
   dotnet add package VibeMQ.Client

Первый сервер
=============

Создайте файл ``Program.cs`` с кодом запуска сервера:

.. code-block:: csharp

   using Microsoft.Extensions.Logging;
   using VibeMQ.Server;
   using VibeMQ.Core.Enums;

   using var loggerFactory = LoggerFactory.Create(builder => {
       builder.SetMinimumLevel(LogLevel.Information).AddConsole();
   });

   // Создаём и настраиваем брокер
   var broker = BrokerBuilder.Create()
       .UsePort(8080)
       .UseAuthentication("my-secret-token")
       .ConfigureQueues(options => {
           options.DefaultDeliveryMode = DeliveryMode.RoundRobin;
           options.MaxQueueSize = 10_000;
           options.EnableAutoCreate = true;
       })
       .UseLoggerFactory(loggerFactory)
       .Build();

   Console.WriteLine("Запуск VibeMQ сервера на порту 8080...");
   
   // Запускаем сервер
   await broker.RunAsync(CancellationToken.None);

Запустите сервер:

.. code-block:: bash

   dotnet run

Первый клиент
=============

Создайте второй проект для клиента:

.. code-block:: bash

   dotnet new console -n VibeMQ.Client.Demo
   cd VibeMQ.Client.Demo
   dotnet add package VibeMQ.Client

Отредактируйте ``Program.cs``:

.. code-block:: csharp

   using Microsoft.Extensions.Logging;
   using VibeMQ.Client;

   using var loggerFactory = LoggerFactory.Create(builder => {
       builder.SetMinimumLevel(LogLevel.Information).AddConsole();
   });

   var logger = loggerFactory.CreateLogger<VibeMQClient>();

   // Подключаемся к серверу
   await using var client = await VibeMQClient.ConnectAsync(
       "localhost",
       8080,
       new ClientOptions {
           AuthToken = "my-secret-token",
           ReconnectPolicy = new ReconnectPolicy {
               MaxAttempts = 5,
               UseExponentialBackoff = true
           }
       },
       logger
   );

   Console.WriteLine("Подключено к VibeMQ серверу!");

   // Подписываемся на очередь "notifications"
   await using var subscription = await client.SubscribeAsync<dynamic>(
       "notifications",
       async msg => {
           Console.WriteLine($"📨 Получено сообщение:");
           Console.WriteLine($"   Заголовок: {msg.Title}");
           Console.WriteLine($"   Текст: {msg.Body}");
           Console.WriteLine($"   Время: {msg.Timestamp}");
       }
   );

   Console.WriteLine("Подписка оформлена. Ожидание сообщений...");
   Console.WriteLine("Нажмите Enter для отправки тестового сообщения.");
   Console.ReadLine();

   // Отправляем тестовое сообщение
   await client.PublishAsync("notifications", new {
       Title = "Привет!",
       Body = "Это первое сообщение в VibeMQ",
       Timestamp = DateTime.Now.ToString("HH:mm:ss")
   });

   Console.WriteLine("Сообщение отправлено!");
   Console.WriteLine("Нажмите Enter для выхода...");
   Console.ReadLine();

Запустите клиента:

.. code-block:: bash

   dotnet run

Полный пример: издатель и подписчик
===================================

Создайте два отдельных приложения:

**Издатель (Publisher.cs):**

.. code-block:: csharp

   using VibeMQ.Client;

   await using var publisher = await VibeMQClient.ConnectAsync("localhost", 8080, new ClientOptions {
       AuthToken = "my-secret-token"
   });

   Console.WriteLine("Издатель подключён. Введите сообщение для отправки:");

   while (true) {
       var input = Console.ReadLine();
       if (string.IsNullOrWhiteSpace(input)) break;

       await publisher.PublishAsync("notifications", new {
           Title = "Новое сообщение",
           Body = input,
           Timestamp = DateTime.Now.ToString("HH:mm:ss")
       });

       Console.WriteLine("✓ Сообщение отправлено");
   }

**Подписчик (Subscriber.cs):**

.. code-block:: csharp

   using VibeMQ.Client;

   await using var subscriber = await VibeMQClient.ConnectAsync("localhost", 8080, new ClientOptions {
       AuthToken = "my-secret-token"
   });

   await using var subscription = await subscriber.SubscribeAsync<dynamic>(
       "notifications",
       async msg => {
           Console.WriteLine($"🔔 {msg.Title}: {msg.Body} (в {msg.Timestamp})");
       }
   );

   Console.WriteLine("Подписчик запущен. Нажмите Enter для выхода...");
   Console.ReadLine();

Запустите сначала подписчика, затем издателя и отправьте несколько сообщений.

Следующие шаги
==============

Теперь, когда вы познакомились с основами, вы можете изучить:

- :doc:`architecture` — как устроен VibeMQ внутри
- :doc:`features` — подробный обзор всех возможностей
- :doc:`server-setup` — детальная настройка сервера
- :doc:`client-usage` — расширенное использование клиента
- :doc:`configuration` — все параметры конфигурации
- :doc:`di-integration` — интеграция с Dependency Injection
