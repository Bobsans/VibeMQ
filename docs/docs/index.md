# VibeMQ Documentation

**VibeMQ** — это простой, но надёжный месседж-брокер на C# с использованием TCP в качестве транспорта. Поддерживает pub/sub, очереди с гарантией доставки, keep-alive, реконнекты, аутентификацию по токену и другие возможности.

## 🚀 Быстрый старт

### Установка

```bash
dotnet add package VibeMQ.Server
dotnet add package VibeMQ.Client
```

### Минимальный пример сервера

```csharp
using VibeMQ.Server;

var broker = BrokerBuilder.Create()
    .UsePort(8080)
    .Build();

await broker.RunAsync();
```

### Минимальный пример клиента

```csharp
using VibeMQ.Client;

await using var client = await VibeMQClient.ConnectAsync("localhost", 8080);

// Публикация
await client.PublishAsync("notifications", new { Title = "Hello", Body = "World" });

// Подписка
await using var subscription = await client.SubscribeAsync<Notification>("notifications", msg => {
    Console.WriteLine($"Received: {msg.Title}");
    return Task.CompletedTask;
});
```

## 📚 Основное содержание

- **[Getting Started](getting-started.md)** — подробное руководство по началу работы
- **[Architecture](architecture.md)** — архитектура VibeMQ
- **[Server](server/broker-server.md)** — документация по серверной части
- **[Client](client/client.md)** — документация по клиентской библиотеке
- **[Protocol](protocol.md)** — описание протокола
- **[Configuration](configuration.md)** — настройка сервера и клиента
- **[Dependency Injection](dependency-injection.md)** — интеграция с DI
- **[Examples](examples.md)** — примеры использования

## 🔗 Ссылки

- [GitHub репозиторий](https://github.com/DarkBoy/VibeMQ)
- [Roadmap](../ROADMAP.md)
- [NuGet пакет VibeMQ.Server](https://www.nuget.org/packages/VibeMQ.Server)
- [NuGet пакет VibeMQ.Client](https://www.nuget.org/packages/VibeMQ.Client)
