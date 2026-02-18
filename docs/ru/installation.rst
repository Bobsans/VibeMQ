============
Установка
============

Это руководство описывает различные способы установки VibeMQ.

.. contents:: Содержание
   :local:
   :depth: 2

Системные требования
====================

**Минимальные требования:**

- ОС: Windows, Linux, macOS
- .NET 8.0 SDK или выше
- ОЗУ: 512 МБ (рекомендуется 1 ГБ+)
- Дисковое пространство: 100 МБ

**Для production:**

- .NET 8.0 Runtime или .NET 10.0
- 2+ ГБ ОЗУ
- SSD для лучшего производительности

Установка через NuGet
=====================

Базовые пакеты
--------------

Для использования VibeMQ в вашем проекте добавьте необходимые пакеты:

.. code-block:: bash

   # Сервер брокера
   dotnet add package VibeMQ.Server

   # Клиент для подключения
   dotnet add package VibeMQ.Client

   # Ядро (модели и интерфейсы)
   dotnet add package VibeMQ.Core

   # Протокол связи
   dotnet add package VibeMQ.Protocol

   # Health check сервер
   dotnet add package VibeMQ.Health

Пакеты для Dependency Injection
-------------------------------

Для интеграции с Microsoft.Extensions.DependencyInjection:

.. code-block:: bash

   # DI для сервера
   dotnet add package VibeMQ.Server.DependencyInjection

   # DI для клиента
   dotnet add package VibeMQ.Client.DependencyInjection

Все пакеты сразу
----------------

Для быстрого добавления всех компонентов:

.. code-block:: bash

   dotnet add package VibeMQ.Server
   dotnet add package VibeMQ.Client
   dotnet add package VibeMQ.Health
   dotnet add package VibeMQ.Server.DependencyInjection
   dotnet add package VibeMQ.Client.DependencyInjection

Установка через Docker
======================

VibeMQ можно запустить в Docker-контейнере.

Dockerfile
----------

Создайте ``Dockerfile`` в вашем проекте:

.. code-block:: dockerfile

   FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
   WORKDIR /app
   EXPOSE 8080 8081

   FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
   WORKDIR /src
   COPY ["VibeMQ.Server.csproj", "."]
   RUN dotnet restore
   COPY . .
   RUN dotnet build -c Release -o /app/build

   FROM build AS publish
   RUN dotnet publish -c Release -o /app/publish

   FROM base AS final
   WORKDIR /app
   COPY --from=publish /app/publish .
   ENTRYPOINT ["dotnet", "VibeMQ.Server.dll"]

Сборка и запуск:

.. code-block:: bash

   docker build -t vibemq-server .
   docker run -p 8080:8080 -p 8081:8081 vibemq-server

Docker Compose
--------------

Для развёртывания с другими сервисами создайте ``docker-compose.yml``:

.. code-block:: yaml

   version: '3.8'

   services:
     vibemq:
       image: vibemq-server:latest
       container_name: vibemq-broker
       ports:
         - "8080:8080"  # TCP порт для клиентов
         - "8081:8081"  # HTTP порт для health checks
       environment:
         - VIBEMQ_PORT=8080
         - VIBEMQ_AUTH_TOKEN=my-secret-token
         - VIBEMQ_MAX_CONNECTIONS=1000
       volumes:
         - vibemq-data:/data
       restart: unless-stopped

     # Пример приложения-клиента
     my-app:
       image: my-app:latest
       depends_on:
         - vibemq
       environment:
         - VIBEMQ_HOST=vibemq
         - VIBEMQ_PORT=8080

   volumes:
     vibemq-data:

Запуск:

.. code-block:: bash

   docker-compose up -d

Установка из исходного кода
===========================

Клонирование репозитория
------------------------

.. code-block:: bash

   git clone https://github.com/DarkBoy/VibeMQ.git
   cd VibeMQ

Сборка проекта
--------------

.. code-block:: bash

   # Сборка всех проектов
   dotnet build -c Release

   # Запуск тестов
   dotnet test

   # Сборка конкретного проекта
   dotnet build src/VibeMQ.Server/VibeMQ.Server.csproj -c Release

Локальная установка пакетов
---------------------------

Для использования локально собранной версии:

.. code-block:: bash

   # Создание локального NuGet источника
   dotnet pack src/VibeMQ.Server -c Release -o ./nupkg
   dotnet pack src/VibeMQ.Client -c Release -o ./nupkg

   # Добавление локального источника
   dotnet nuget add source ./nupkg --name LocalVibeMQ

   # Установка из локального источника
   dotnet add package VibeMQ.Server --source LocalVibeMQ

Интеграция в существующий проект
================================

ASP.NET Core приложение
-----------------------

Для ASP.NET Core приложений используйте DI-интеграцию:

.. code-block:: csharp

   using VibeMQ.Server.DependencyInjection;
   using VibeMQ.Client.DependencyInjection;

   var builder = WebApplication.CreateBuilder(args);

   // Добавляем сервер брокера
   builder.Services.AddVibeMQBroker(options => {
       options.Port = 8080;
       options.EnableAuthentication = true;
       options.AuthToken = builder.Configuration["VibeMQ:AuthToken"];
   });

   // Добавляем клиента для отправки сообщений
   builder.Services.AddVibeMQClient(settings => {
       settings.Host = "localhost";
       settings.Port = 8080;
       settings.ClientOptions.AuthToken = builder.Configuration["VibeMQ:AuthToken"];
   });

   var app = builder.Build();
   await app.RunAsync();

Worker Service
--------------

Для фоновых сервисов (.NET Worker):

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

Консольное приложение
---------------------

Для консольных приложений используйте прямой вызов:

.. code-block:: csharp

   using VibeMQ.Server;

   var broker = BrokerBuilder.Create()
       .UsePort(8080)
       .Build();

   await broker.RunAsync(CancellationToken.None);

Проверка установки
==================

Проверка сервера
----------------

После запуска сервера проверьте health check:

.. code-block:: bash

   curl http://localhost:8081/health/

Ответ должен быть:

.. code-block:: json

   {
     "status": "healthy",
     "active_connections": 0,
     "queue_count": 0,
     "memory_usage_mb": 128
   }

Проверка клиента
----------------

Создайте тестовый скрипт:

.. code-block:: csharp

   using VibeMQ.Client;

   try {
       await using var client = await VibeMQClient.ConnectAsync("localhost", 8080);
       Console.WriteLine("✓ Подключение успешно!");
       Console.WriteLine($"Статус: {(client.IsConnected ? "Подключено" : "Отключено")}");
   } catch (Exception ex) {
       Console.WriteLine($"✗ Ошибка: {ex.Message}");
   }

Устранение проблем
==================

Порт уже занят
--------------

**Ошибка:** ``Address already in use``

**Решение:** Измените порт в конфигурации:

.. code-block:: csharp

   var broker = BrokerBuilder.Create()
       .UsePort(8081)  // Используйте другой порт
       .Build();

Ошибка аутентификации
---------------------

**Ошибка:** ``Authentication failed``

**Решение:** Убедитесь, что токены совпадают:

.. code-block:: csharp

   // Сервер
   .UseAuthentication("my-token")

   // Клиент
   new ClientOptions { AuthToken = "my-token" }

TLS/SSL ошибки
--------------

**Ошибка:** ``The remote certificate is invalid``

**Решение:** Для тестов отключите валидацию:

.. code-block:: csharp

   new ClientOptions {
       UseTls = true,
       SkipCertificateValidation = true  // Только для тестов!
   }

Для production используйте валидные сертификаты.

Следующие шаги
==============

- :doc:`getting-started` — быстрое начало работы
- :doc:`server-setup` — настройка сервера
- :doc:`configuration` — параметры конфигурации
