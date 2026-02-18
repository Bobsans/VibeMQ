========
Примеры
========

Это руководство содержит практические примеры использования VibeMQ.

.. contents:: Содержание
   :local:
   :depth: 2

Базовые примеры
===============

Простой издатель
----------------

.. code-block:: csharp

   using Microsoft.Extensions.Logging;
   using VibeMQ.Client;

   using var loggerFactory = LoggerFactory.Create(builder => {
       builder.SetMinimumLevel(LogLevel.Information).AddConsole();
   });

   var logger = loggerFactory.CreateLogger<VibeMQClient>();

   await using var publisher = await VibeMQClient.ConnectAsync(
       "localhost",
       8080,
       new ClientOptions { AuthToken = "my-token" },
       logger
   );

   Console.WriteLine("Издатель подключён. Введите сообщение (Enter для выхода):");

   while (true) {
       var input = Console.ReadLine();
       if (string.IsNullOrWhiteSpace(input)) break;

       await publisher.PublishAsync("messages", new {
           Text = input,
           Timestamp = DateTime.Now.ToString("HH:mm:ss")
       });

       Console.WriteLine("✓ Сообщение отправлено");
   }

Простой подписчик
-----------------

.. code-block:: csharp

   using Microsoft.Extensions.Logging;
   using VibeMQ.Client;

   using var loggerFactory = LoggerFactory.Create(builder => {
       builder.SetMinimumLevel(LogLevel.Information).AddConsole();
   });

   var logger = loggerFactory.CreateLogger<VibeMQClient>();

   await using var subscriber = await VibeMQClient.ConnectAsync(
       "localhost",
       8080,
       new ClientOptions { AuthToken = "my-token" },
       logger
   );

   await using var subscription = await subscriber.SubscribeAsync<dynamic>(
       "messages",
       async msg => {
           Console.WriteLine($"📨 {msg.Text} (в {msg.Timestamp})");
       }
   );

   Console.WriteLine("Подписчик запущен. Нажмите Enter для выхода...");
   Console.ReadLine();

Сервер
------

.. code-block:: csharp

   using Microsoft.Extensions.Logging;
   using VibeMQ.Server;
   using VibeMQ.Core.Enums;

   using var loggerFactory = LoggerFactory.Create(builder => {
       builder.SetMinimumLevel(LogLevel.Information).AddConsole();
   });

   var broker = BrokerBuilder.Create()
       .UsePort(8080)
       .UseAuthentication("my-token")
       .ConfigureQueues(options => {
           options.DefaultDeliveryMode = DeliveryMode.RoundRobin;
           options.MaxQueueSize = 10_000;
           options.EnableAutoCreate = true;
       })
       .ConfigureHealthChecks(options => {
           options.Enabled = true;
           options.Port = 8081;
       })
       .UseLoggerFactory(loggerFactory)
       .Build();

   Console.WriteLine("Запуск VibeMQ сервера...");
   await broker.RunAsync(CancellationToken.None);

Очереди задач
=============

Обработка заказов
-----------------

**Издатель (OrderService.cs):**

.. code-block:: csharp

   using VibeMQ.Client;

   public class OrderService {
       private readonly VibeMQClient _client;

       public OrderService(VibeMQClient client) {
           _client = client;
       }

       public async Task CreateOrderAsync(Order order) {
           // Сохранение заказа в БД
           await SaveOrderAsync(order);

           // Публикация события
           await _client.PublishAsync("orders.created", new {
               OrderId = order.Id,
               Amount = order.Amount,
               CustomerId = order.CustomerId,
               CreatedAt = DateTime.UtcNow
           });
       }

       private Task SaveOrderAsync(Order order) {
           // Имитация сохранения
           return Task.CompletedTask;
       }
   }

   public class Order {
       public string Id { get; set; }
       public decimal Amount { get; set; }
       public string CustomerId { get; set; }
   }

**Обработчик (OrderProcessor.cs):**

.. code-block:: csharp

   using VibeMQ.Client;

   public class OrderProcessor : BackgroundService {
       private readonly IVibeMQClientFactory _clientFactory;
       private readonly ILogger<OrderProcessor> _logger;

       public OrderProcessor(
           IVibeMQClientFactory clientFactory,
           ILogger<OrderProcessor> logger) {
           _clientFactory = clientFactory;
           _logger = logger;
       }

       protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
           await using var client = await _clientFactory.CreateAsync(stoppingToken);

           await using var subscription = await client.SubscribeAsync<dynamic>(
               "orders.created",
               async order => {
                   _logger.LogInformation(
                       "Обработка заказа {OrderId} на сумму {Amount}",
                       order.OrderId,
                       order.Amount
                   );

                   try {
                       await ProcessOrderAsync(order);
                       _logger.LogInformation("Заказ {OrderId} обработан", order.OrderId);
                   } catch (Exception ex) {
                       _logger.LogError(ex, "Ошибка обработки заказа {OrderId}", order.OrderId);
                       throw;  // Для повторной попытки
                   }
               },
               stoppingToken
           );

           _logger.LogInformation("OrderProcessor запущен");

           try {
               await Task.Delay(Timeout.Infinite, stoppingToken);
           } catch (OperationCanceledException) {
               _logger.LogInformation("OrderProcessor остановлен");
           }
       }

       private Task ProcessOrderAsync(dynamic order) {
           // Обработка заказа
           return Task.CompletedTask;
       }
   }

**Регистрация в DI:**

.. code-block:: csharp

   using VibeMQ.Client.DependencyInjection;

   var host = Host.CreateDefaultBuilder(args)
       .ConfigureServices(services => {
           services.AddVibeMQClient(settings => {
               settings.Host = "localhost";
               settings.Port = 8080;
               settings.ClientOptions.AuthToken = "my-token";
           });

           services.AddHostedService<OrderProcessor>();
       })
       .Build();

   await host.RunAsync();

Фоновые задачи с приоритетами
-----------------------------

.. code-block:: csharp

   using VibeMQ.Client;
   using VibeMQ.Core.Enums;

   public class TaskQueueService {
       private readonly VibeMQClient _client;

       public TaskQueueService(VibeMQClient client) {
           _client = client;
       }

       public async Task EnqueueTaskAsync(TaskData task, MessagePriority priority) {
           await _client.PublishAsync("tasks", task, options => {
               options.Priority = priority;
               options.Headers = new Dictionary<string, string> {
                   ["task_type"] = task.Type,
                   ["created_at"] = DateTime.UtcNow.ToString("O")
               };
           });
       }
   }

   public class TaskData {
       public string Id { get; set; }
       public string Type { get; set; }
       public Dictionary<string, object> Data { get; set; }
   }

**Использование:**

.. code-block:: csharp

   // Обычная задача
   await taskQueue.EnqueueTaskAsync(new TaskData {
       Id = "task_1",
       Type = "email",
       Data = new { To = "user@example.com" }
   }, MessagePriority.Normal);

   // Критическая задача
   await taskQueue.EnqueueTaskAsync(new TaskData {
       Id = "task_2",
       Type = "payment",
       Data = new { Amount = 100 }
   }, MessagePriority.Critical);

Шина событий
============

Реализация EventBus
-------------------

.. code-block:: csharp

   using VibeMQ.Client.DependencyInjection;

   public interface IEventBus {
       Task PublishAsync<T>(string eventType, T eventData, CancellationToken ct = default);
       IDisposable SubscribeAsync<T>(string eventType, Func<T, Task> handler);
   }

   public class VibeMQEventBus : IEventBus {
       private readonly IVibeMQClientFactory _clientFactory;
       private readonly ILogger<VibeMQEventBus> _logger;
       private readonly List<IAsyncDisposable> _subscriptions = new();

       public VibeMQEventBus(
           IVibeMQClientFactory clientFactory,
           ILogger<VibeMQEventBus> logger) {
           _clientFactory = clientFactory;
           _logger = logger;
       }

       public async Task PublishAsync<T>(string eventType, T eventData, CancellationToken ct = default) {
           await using var client = await _clientFactory.CreateAsync(ct);

           await client.PublishAsync($"events.{eventType}", eventData, options => {
               options.Headers = new Dictionary<string, string> {
                   ["event_type"] = eventType,
                   ["timestamp"] = DateTime.UtcNow.ToString("O")
               };
           });

           _logger.LogInformation("Событие {EventType} опубликовано", eventType);
       }

       public IDisposable SubscribeAsync<T>(string eventType, Func<T, Task> handler) {
           // Асинхронная подписка
           var subscriptionTask = SubscribeInternalAsync<T>(eventType, handler);
           return new AsyncDisposableWrapper(subscriptionTask);
       }

       private async Task<IAsyncDisposable> SubscribeInternalAsync<T>(
           string eventType,
           Func<T, Task> handler) {
           
           var client = await _clientFactory.CreateAsync();
           
           var subscription = await client.SubscribeAsync<T>(
               $"events.{eventType}",
               async eventData => {
                   _logger.LogInformation("Получено событие {EventType}", eventType);
                   await handler(eventData);
               }
           );

           _subscriptions.Add(subscription);
           return subscription;
       }
   }

   // Обёртка для async disposable
   public class AsyncDisposableWrapper : IDisposable {
       private readonly Task<IAsyncDisposable> _subscriptionTask;
       private IAsyncDisposable? _subscription;

       public AsyncDisposableWrapper(Task<IAsyncDisposable> subscriptionTask) {
           _subscriptionTask = subscriptionTask;
       }

       public void Dispose() {
           _ = Task.Run(async () => {
               if (_subscription == null) {
                   _subscription = await _subscriptionTask;
               }
               await _subscription.DisposeAsync();
           });
       }
   }

**Регистрация:**

.. code-block:: csharp

   services.AddSingleton<IEventBus, VibeMQEventBus>();

**Использование:**

.. code-block:: csharp

   public class OrderService {
       private readonly IEventBus _eventBus;

       public OrderService(IEventBus eventBus) {
           _eventBus = eventBus;
       }

       public async Task CreateOrderAsync(Order order) {
           await SaveOrderAsync(order);

           await _eventBus.PublishAsync("order.created", new {
               OrderId = order.Id,
               Amount = order.Amount
           });

           await _eventBus.PublishAsync("order.validated", new {
               OrderId = order.Id,
               ValidatedAt = DateTime.UtcNow
           });
       }
   }

   public class EmailService {
       public EmailService(IEventBus eventBus) {
           eventBus.SubscribeAsync<OrderCreated>("order.created", async order => {
               await SendOrderConfirmationEmailAsync(order);
           });
       }
   }

Микросервисы
============

Синхронизация данных между сервисами
------------------------------------

**Сервис пользователей (Users.cs):**

.. code-block:: csharp

   public class UserService {
       private readonly IEventBus _eventBus;

       public UserService(IEventBus eventBus) {
           _eventBus = eventBus;
       }

       public async Task RegisterUserAsync(User user) {
           await SaveUserAsync(user);

           await _eventBus.PublishAsync("user.registered", new {
               UserId = user.Id,
               Email = user.Email,
               RegisteredAt = DateTime.UtcNow
           });
       }
   }

**Сервис уведомлений (Notifications.cs):**

.. code-block:: csharp

   public class NotificationService : BackgroundService {
       private readonly IEventBus _eventBus;
       private readonly ILogger<NotificationService> _logger;

       public NotificationService(IEventBus eventBus, ILogger<NotificationService> logger) {
           _eventBus = eventBus;
           _logger = logger;
       }

       protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
           await _eventBus.SubscribeAsync<dynamic>("user.registered", async data => {
               _logger.LogInformation(
                   "Отправка welcome email пользователю {Email}",
                   data.Email
               );

               await SendWelcomeEmailAsync(data.Email);
           });

           await Task.Delay(Timeout.Infinite, stoppingToken);
       }
   }

**Сервис аналитики (Analytics.cs):**

.. code-block:: csharp

   public class AnalyticsService : BackgroundService {
       private readonly IEventBus _eventBus;
       private readonly ILogger<AnalyticsService> _logger;

       public AnalyticsService(IEventBus eventBus, ILogger<AnalyticsService> logger) {
           _eventBus = eventBus;
           _logger = logger;
       }

       protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
           await _eventBus.SubscribeAsync<dynamic>("user.registered", async data => {
               _logger.LogInformation("Регистрация пользователя: {UserId}", data.UserId);
               await TrackEventAsync("user_registered", data);
           });

           await _eventBus.SubscribeAsync<dynamic>("order.created", async data => {
               _logger.LogInformation("Создан заказ: {OrderId}", data.OrderId);
               await TrackEventAsync("order_created", data);
           });

           await Task.Delay(Timeout.Infinite, stoppingToken);
       }
   }

CQRS с VibeMQ
-------------

**Commands:**

.. code-block:: csharp

   public class CommandBus {
       private readonly VibeMQClient _client;

       public CommandBus(VibeMQClient client) {
           _client = client;
       }

       public async Task SendAsync<T>(string commandName, T command) {
           await _client.PublishAsync($"commands.{commandName}", command, options => {
               options.Headers = new Dictionary<string, string> {
                   ["command_type"] = typeof(T).Name,
                   ["correlation_id"] = Guid.NewGuid().ToString()
               };
           });
       }
   }

**Queries:**

.. code-block:: csharp

   public class QueryBus {
       private readonly VibeMQClient _client;

       public QueryBus(VibeMQClient client) {
           _client = client;
       }

       public async Task<TResponse> AskAsync<TResponse>(string queryName, object query) {
           var correlationId = Guid.NewGuid().ToString();
           var tcs = new TaskCompletionSource<TResponse>();

           // Временная подписка для ответа
           var subscription = await _client.SubscribeAsync<TResponse>(
               $"queries.{queryName}.response",
               msg => {
                   if (msg.Headers?["correlation_id"] == correlationId) {
                       tcs.SetResult(msg);
                   }
                   return Task.CompletedTask;
               }
           );

           // Отправка запроса
           await _client.PublishAsync($"queries.{queryName}", query, options => {
               options.Headers = new Dictionary<string, string> {
                   ["correlation_id"] = correlationId,
                   ["reply_to"] = $"queries.{queryName}.response"
               };
           });

           // Ожидание ответа с таймаутом
           using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
           cts.Token.Register(() => {
               tcs.TrySetException(new TimeoutException("Query timeout"));
               subscription.DisposeAsync();
           });

           return await tcs.Task;
       }
   }

Мониторинг и логирование
========================

Кастомный логгер
----------------

.. code-block:: csharp

   public class FileLogger : ILogger {
       private readonly string _filePath;
       private readonly SemaphoreSlim _semaphore = new(1, 1);

       public FileLogger(string filePath) {
           _filePath = filePath;
       }

       public IDisposable BeginScope<TState>(TState state) => null;

       public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

       public void Log<TState>(
           LogLevel logLevel,
           EventId eventId,
           TState state,
           Exception exception,
           Func<TState, Exception, string> formatter) {
           
           if (!IsEnabled(logLevel)) return;

           _ = Task.Run(async () => {
               await _semaphore.WaitAsync();
               try {
                   var message = $"{DateTime.Now:O} [{logLevel}] {formatter(state, exception)}";
                   await File.AppendAllTextAsync(_filePath, message + Environment.NewLine);
               } finally {
                   _semaphore.Release();
               }
           });
       }
   }

   public class FileLoggerProvider : ILoggerProvider {
       private readonly string _filePath;

       public FileLoggerProvider(string filePath) {
           _filePath = filePath;
       }

       public ILogger CreateLogger(string categoryName) => new FileLogger(_filePath);

       public void Dispose() { }
   }

**Использование:**

.. code-block:: csharp

   using var loggerFactory = new FileLoggerProvider("logs/vibemq.log");

   var broker = BrokerBuilder.Create()
       .UsePort(8080)
       .UseLoggerFactory(loggerFactory)
       .Build();

Метрики приложения
------------------

.. code-block:: csharp

   public class AppMetrics {
       private readonly IBrokerMetrics _brokerMetrics;
       private readonly ILogger<AppMetrics> _logger;
       private readonly Timer _timer;

       public AppMetrics(
           IBrokerMetrics brokerMetrics,
           ILogger<AppMetrics> logger) {
           _brokerMetrics = brokerMetrics;
           _logger = logger;

           _timer = new Timer(_ => LogMetrics(), null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
       }

       private void LogMetrics() {
           var snapshot = _brokerMetrics.GetSnapshot();

           _logger.LogInformation(
               "Метрики: Опубликовано={Published}, Доставлено={Delivered}, " +
               "Подключений={Connections}, Латентность={Latency:F2}ms",
               snapshot.TotalMessagesPublished,
               snapshot.TotalMessagesDelivered,
               snapshot.ActiveConnections,
               snapshot.AverageDeliveryLatencyMs
           );
       }

       public void Dispose() => _timer.Dispose();
   }

Следующие шаги
==============

- :doc:`getting-started` — быстрое начало
- :doc:`client-usage` — использование клиента
- :doc:`di-integration` — интеграция с DI
