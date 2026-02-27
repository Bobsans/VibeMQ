# Подключение через Connection String

**Описание:** Единая строка подключения к брокеру VibeMQ для упрощения конфигурации (переменные окружения, конфиг, код).

**Связанные типы:** [VibeMQClientSettings](src/VibeMQ.Client.DependencyInjection/VibeMQClientSettings.cs), [ClientOptions](src/VibeMQ.Client/ClientOptions.cs), [VibeMQClient.ConnectAsync](src/VibeMQ.Client/VibeMQClient.cs).

---

## 1. Цели

- Позволить задавать параметры подключения одной строкой (например `VIBEMQ_CONNECTION_STRING` или `ConnectionStrings:VibeMQ`).
- Сохранить совместимость с текущим API: `ConnectAsync(host, port, options)` и `AddVibeMQClient(configure)`.
- Поддержать парсинг из конфигурации (IConfiguration) и из строки в коде.

---

## 2. Формат Connection String

### 2.1 Рекомендуемый формат: URL-style

Формат по аналогии с Redis, PostgreSQL, RabbitMQ:

```
vibemq://[username[:password]@]host[:port][?query]
```

**Правила:**

- **Схема:** `vibemq` (без учёта регистра).
- **Authority:** `[username[:password]@]host[:port]`.
  - `username` и `password` — опциональны; если брокер с включённой авторизацией, задаются для логина.
  - Спецсимволы в `username`/`password` кодируются через URL encoding (например `%40` для `@`).
  - `host` — обязателен (localhost, IP, имя хоста).
  - `port` — опционален; по умолчанию **2925**.
- **Query string** — опциональные параметры в виде `key=value`, разделённые `&`. Регистр ключей не учитывается.

**Примеры:**

```
vibemq://localhost
vibemq://localhost:2925
vibemq://user:secret@broker.example.com:2925
vibemq://broker.example.com?tls=true&skipCertValidation=true
vibemq://admin%40domain:p%40ss@host:2925?keepAlive=60&compression=brotli,gzip
```

### 2.2 Альтернативный формат: key=value (ADO.NET-style)

Для конфигурации в `appsettings.json` или `ConnectionStrings:VibeMQ` удобен формат с разделителем `;`:

```
Host=localhost;Port=2925;Username=user;Password=secret;UseTls=true;SkipCertificateValidation=false;KeepAliveInterval=30;CommandTimeout=10;Compression=brotli,gzip;CompressionThreshold=1024;ReconnectMaxAttempts=10;ReconnectInitialDelay=1;ReconnectMaxDelay=300;ReconnectExponentialBackoff=true
```

**Правила:**

- Пары `Key=Value`, разделитель `;`.
- В значении символы `;` и `=` экранируются (например удвоением или обратным слэшем — см. раздел 2.4).
- Имена ключей без учёта регистра.

### 2.3 Соответствие параметров URL query и key=value

| Параметр (query / key) | Тип / по умолчанию | Описание | Маппинг в код |
|------------------------|--------------------|----------|----------------|
| `tls` / `UseTls` | bool, default: false | Включить TLS | `ClientOptions.UseTls` |
| `skipCertValidation` / `SkipCertificateValidation` | bool, default: false | Не проверять сертификат сервера | `ClientOptions.SkipCertificateValidation` |
| `keepAlive` / `KeepAliveInterval` | int (секунды), default: 30 | Интервал keep-alive | `ClientOptions.KeepAliveInterval` |
| `commandTimeout` / `CommandTimeout` | int (секунды), default: 10 | Таймаут ожидания ответа брокера | `ClientOptions.CommandTimeout` |
| `compression` / `Compression` | строка: `none`, `brotli`, `gzip` или `brotli,gzip` (приоритет через запятую) | Алгоритмы сжатия | `ClientOptions.PreferredCompressions` |
| `compressionThreshold` / `CompressionThreshold` | int (байты), default: 1024 | Порог размера тела для сжатия | `ClientOptions.CompressionThreshold` |
| `reconnectMaxAttempts` / `ReconnectMaxAttempts` | int, default: unlimited (например 0 = без лимита) | Макс. попыток переподключения | `ReconnectPolicy.MaxAttempts` |
| `reconnectInitialDelay` / `ReconnectInitialDelay` | int (секунды), default: 1 | Начальная задержка перед переподключением | `ReconnectPolicy.InitialDelay` |
| `reconnectMaxDelay` / `ReconnectMaxDelay` | int (секунды), default: 300 | Макс. задержка между попытками | `ReconnectPolicy.MaxDelay` |
| `reconnectExponentialBackoff` / `ReconnectExponentialBackoff` | bool, default: true | Экспоненциальный бэкхофф | `ReconnectPolicy.UseExponentialBackoff` |
| `queues` / `Queues` | строка: имена очередей через запятую | Очереди для declare-on-connect (простой сценарий) | См. раздел 2.5 |

В URL в authority передаются только `username` и `password` (и host/port); остальное — в query. В key=value все параметры задаются явно, включая `Host`, `Port`, `Username`, `Password`.

### 2.4 Экранирование и парсинг

- **URL:** стандартное URL-кодирование для username/password и для значений в query (например `%3D` для `=`).
- **Key=value:** значения в кавычках допускаются для включения `;` и `=`. Без кавычек — значение до следующего `;`. Обратный слэш `\` для экранирования следующего символа (опционально, для единообразия с другими .NET провайдерами).
- При неверном формате или неизвестном ключе — явное исключение с сообщением (например `ArgumentException` или кастомный `VibeMQConnectionStringException`).

### 2.5 Очереди (declare-on-connect)

В connection string поддерживается только простой список имён очередей для автоматического объявления при подключении:

- Параметр: `queues=orders,notifications,events`.
- Маппинг: для каждого имени создаётся `QueueDeclaration` с дефолтными `QueueOptions`, `OnConflict=Ignore`, `FailOnProvisioningError=true`. Сложные сценарии (DLQ, overflow, приоритеты) по-прежнему настраиваются в коде через `ClientOptions.DeclareQueue(...)`.

---

## 3. API и точки входа

### 3.1 Парсинг в объекты

- **Новый класс:** `VibeMQConnectionString` или статический парсер в `VibeMQ.Client`.
  - Метод: `Parse(string connectionString)` → возвращает DTO или сразу `(string host, int port, VibeMQClientSettings settings)` / `(string host, int port, ClientOptions options)`.
  - Перегрузка: `TryParse(string connectionString, out ...)` для безопасного парсинга.
- Определение формата: если строка начинается с `vibemq://` — парсим как URL; иначе — как key=value.

### 3.2 Подключение по строке

- **VibeMQClient:** новый метод (или перегрузка):
  - `VibeMQClient.ConnectAsync(string connectionString, ILogger<VibeMQClient>? logger = null, CancellationToken cancellationToken = default)`.
  - Внутри: парсинг строки → `host`, `port`, `ClientOptions` → вызов существующего `ConnectAsync(host, port, options, ...)`.

### 3.3 DI (AddVibeMQClient)

- Перегрузка: `AddVibeMQClient(IServiceCollection services, string connectionString)`.
  - Парсинг строки → заполнение `VibeMQClientSettings` (Host, Port, ClientOptions) → вызов существующего `AddVibeMQClient(services, configure)`.
- Опционально: привязка из конфигурации:
  - `AddVibeMQClient(services, configuration)` — читает ключ, например `ConnectionStrings:VibeMQ` или `VibeMQ:Client:ConnectionString`, парсит и настраивает клиент.
  - Либо отдельный метод `ConfigureVibeMQClient(IConfigurationSection)` для гибкости.

### 3.4 Конфигурация (appsettings / env)

- В хосте приложения строка может задаваться:
  - `ConnectionStrings__VibeMQ=vibemq://user:pass@host:2925` (env).
  - В `appsettings.json`: `"ConnectionStrings": { "VibeMQ": "vibemq://..." }` или отдельная секция `"VibeMQ": { "Client": { "ConnectionString": "..." } }`.
- Документация: описать в [docs](docs/docs/) использование connection string и приоритет над отдельными ключами (Host/Port и т.д.), если заданы оба.

---

## 4. План реализации

### Этап 1: Парсер и маппинг

**Файлы:** новый модуль в `src/VibeMQ.Client/` (например `ConnectionStringParser.cs` или `VibeMQConnectionString.cs`).

**Задачи:**

1. Реализовать парсинг URL-style (`vibemq://...`): извлечь host, port, user, password, query.
2. Реализовать парсинг key=value: разбор пар Key=Value с учётом кавычек и экранирования.
3. Маппинг распарсенных параметров в `ClientOptions` и (host, port) для `VibeMQClientSettings`.
4. Обработка `compression`: парсинг строки `brotli,gzip` / `none` в `IReadOnlyList<CompressionAlgorithm>`.
5. Обработка `queues`: разбить по запятой, создать список `QueueDeclaration` с дефолтными настройками.
6. Unit-тесты: валидные строки, невалидные строки, экранирование, граничные значения (пустой port = 2925, пустой compression = default Brotli,GZip).

### Этап 2: ConnectAsync(connectionString)

**Файлы:** [VibeMQClient.cs](src/VibeMQ.Client/VibeMQClient.cs).

**Задачи:**

1. Добавить перегрузку `ConnectAsync(string connectionString, ILogger<VibeMQClient>? = null, IServiceProvider? = null, CancellationToken = default)`.
2. Внутри: вызов парсера → `ConnectAsync(host, port, options, logger, serviceProvider, cancellationToken)`.
3. Документация в XML и в [docs](docs/docs/).

### Этап 3: DI и конфигурация

**Файлы:** [ServiceCollectionExtensions.cs](src/VibeMQ.Client.DependencyInjection/ServiceCollectionExtensions.cs), при необходимости новый extension.

**Задачи:**

1. Добавить `AddVibeMQClient(IServiceCollection, string connectionString)`.
2. Опционально: `AddVibeMQClient(IServiceCollection, IConfiguration)` — чтение connection string из конфига (например `ConnectionStrings:VibeMQ` или `VibeMQ:Client:ConnectionString`) и вызов перегрузки со строкой.
3. Уточнить приоритет: если заданы и connection string, и `Configure(settings)`, то либо connection string перезаписывает только недостающие поля, либо connection string имеет приоритет полностью (рекомендация: полный приоритет connection string при наличии).
4. Обновить примеры (например [VibeMQ.Example.Client.DI](examples/VibeMQ.Example.Client.DI)) и документацию.

### Этап 4: Документация и переводы

**Файлы:** [docs/docs/](docs/docs/) (конфигурация клиента, getting started), [changelog.rst](docs/docs/changelog.rst), при необходимости [locale](docs/locale/).

**Задачи:**

1. Описать формат connection string (URL и key=value), примеры, переменные окружения.
2. Описать новый API: `ConnectAsync(connectionString)`, `AddVibeMQClient(connectionString)` и привязку из IConfiguration.
3. Добавить запись в changelog при релизе.
4. Обновить русские переводы по правилам проекта.

---

## 5. Критерии готовности

- Поддержка обоих форматов (URL и key=value) с однозначным определением по префиксу `vibemq://`.
- Все текущие параметры `ClientOptions` и `VibeMQClientSettings` (Host, Port, auth, TLS, compression, keep-alive, reconnect) задаваемы через connection string.
- Очереди — хотя бы простой список имён через `queues=...`.
- Обратная совместимость: существующие вызовы `ConnectAsync(host, port, options)` и `AddVibeMQClient(configure)` не меняются.
- Unit-тесты на парсер и интеграционный тест: подключение по connection string к реальному брокеру (в т.ч. с auth и TLS при наличии).
- Документация и changelog обновлены.

---

## 6. Риски и упрощения

- **Сложные QueueDeclarations:** в connection string не поддерживаем полные опции (DLQ, overflow и т.д.) — только имена. Это осознанное упрощение; сложные сценарии остаются в коде.
- **Секреты:** пароль в строке в открытом виде. Рекомендация в документации: не логировать connection string, в production использовать секреты/переменные окружения и ограничивать права на конфиг.
- **Obsolete AuthToken:** в connection string можно не добавлять поддержку `authToken` (устаревший способ); при необходимости добавить позже отдельным параметром.
