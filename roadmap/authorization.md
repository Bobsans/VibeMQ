# Гранулярная авторизация

**Описание:** Многопользовательская аутентификация (username + password) и система разрешений
на основе glob-паттернов очередей. Данные пользователей и разрешений хранятся в выделенной
SQLite-базе на уровне сервера — независимо от основного storage provider.

---

## Текущее состояние

- `IAuthenticationService.AuthenticateAsync(string token)` → `bool` — нет понятия «пользователь»
- `ClientConnection.IsAuthenticated` — только `bool`, нет идентификатора клиента
- `BrokerOptions.AuthToken` — один общий токен на всех клиентов
- Аутентифицированным клиентам разрешено всё без каких-либо проверок доступа
- Ни один обработчик не выполняет проверок: publish, subscribe, create, delete, list — без ограничений

---

## Принятые решения

| # | Вопрос | Решение |
|---|---|---|
| A1 | Модель идентификации | **Username + Password** (BCrypt-хэш в SQLite) |
| A2 | Модель разрешений | **ACL по очередям без ролей** — каждому пользователю назначаются операции на паттерн очереди |
| A3 | Паттерны очередей | **Glob** (`*` = любые символы, в т.ч. `.`). Несколько совпавших паттернов — union разрешений |
| A4 | Хранение | **Выделенная SQLite** на уровне сервера, вне `IStorageProvider`. Конфиг задаёт только пароль суперюзера |
| A5 | ListQueues | **Filtered** — возвращает только очереди с хотя бы одним разрешением у пользователя |

---

## Дизайн

### SQLite-схема авторизации

Отдельная база `auth.db`, управляется `IAuthRepository`. Не зависит от `IStorageProvider`
(который в будущем может быть Redis или RocksDB).

```sql
CREATE TABLE IF NOT EXISTS users (
    username     TEXT    PRIMARY KEY,
    password_hash TEXT   NOT NULL,       -- BCrypt
    is_superuser INTEGER NOT NULL DEFAULT 0,
    created_at   INTEGER NOT NULL,       -- Unix timestamp
    updated_at   INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS permissions (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    username      TEXT    NOT NULL REFERENCES users(username) ON DELETE CASCADE,
    queue_pattern TEXT    NOT NULL,      -- glob: "orders", "orders.*", "*"
    operations    TEXT    NOT NULL,      -- JSON: ["Publish","Subscribe",...]
    created_at    INTEGER NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_permissions_username ON permissions(username);
```

### Суперюзер

Единственная конфигурация авторизации в `BrokerOptions`:

```csharp
public class AuthorizationOptions
{
    /// <summary>
    /// Имя суперюзера. По умолчанию "admin".
    /// </summary>
    public string SuperuserUsername { get; set; } = "admin";

    /// <summary>
    /// Пароль суперюзера при первом запуске. После создания аккаунта в SQLite
    /// значение в конфиге игнорируется — смена только через API.
    /// </summary>
    public string SuperuserPassword { get; set; } = "";
}
```

При первом запуске сервера:
```
users таблица пуста?
  → CreateUser(SuperuserUsername, BCrypt(SuperuserPassword), is_superuser=1)
Пользователь уже есть?
  → ничего не делаем (конфиг игнорируется)
```

Суперюзер имеет все разрешения на всё — проверяется через `is_superuser = 1`,
без записей в таблице `permissions`.

### Glob-матчинг

- `*` матчит любую строку, включая `.` (нет концепции сегментов)
- При нескольких совпавших паттернах — **union** разрешений

```
Паттерны пользователя:
  "orders.*"  → [Publish]
  "*.dlq"     → [Subscribe]
  "orders.dlq" — совпадает с обоими → итого [Publish, Subscribe]

Проверка: IsAuthorized("orders.new", Publish)
  → "orders.*" совпадает → Publish ∈ разрешениях → true

Проверка: IsAuthorized("payments.new", Publish)
  → ни один паттерн не совпал → false
```

### Операции

```csharp
public enum QueueOperation
{
    Publish,
    Subscribe,
    CreateQueue,
    DeleteQueue,
    PurgeQueue,
    GetQueueInfo,
    ListQueues,    // используется только для filtered ListQueues
}
```

### Connect-хендшейк

Протокол: клиент передаёт `username` + `password` в полях Connect-сообщения.

```
Client → Server:  Connect { username: "service-orders", password: "secret" }
Server → Client:  ConnectAck { ... }          — OK
                  Error { AUTH_FAILED }        — неверный пароль / нет пользователя
```

Обратная совместимость:
- `EnableAuthentication = false` → всё как сейчас, анонимный доступ
- `BrokerOptions.AuthToken` помечается `[Obsolete]`, при включённом username/password игнорируется с предупреждением в логе

### Авторизация в обработчиках

`IAuthorizationService` не регистрируется если авторизация не включена — проверка через null:

```csharp
// В каждом обработчике:
if (_authz is not null && !await _authz.IsAuthorizedAsync(
        connection.Username!, QueueOperation.Publish, queueName, ct))
{
    await writer.WriteErrorAsync(ErrorCodes.NotAuthorized, "Access denied", ct);
    return;
}
```

### Filtered ListQueues

```
ListQueuesHandler:
1. Получить все очереди из QueueManager
2. Если суперюзер → вернуть все
3. Иначе: загрузить permissions пользователя
4. Для каждой очереди: matchAny(queueName, userPatterns) → включить если true
5. Вернуть отфильтрованный список
```

### Кэширование разрешений

Permissions загружаются из SQLite **один раз при аутентификации** и кэшируются в `ClientConnection`
на время сессии. При изменении разрешений через Admin API активные сессии не пересчитываются
автоматически (потребуется переподключение). Это допустимо для v1.

```csharp
public sealed class ClientConnection
{
    public bool IsAuthenticated { get; set; }
    public string? Username { get; set; }
    public bool IsSuperuser { get; set; }

    // Кэш permissions для текущей сессии
    // Key: (queuePattern, operations)
    internal IReadOnlyList<PermissionEntry> CachedPermissions { get; set; } = [];
}
```

### Admin-команды протокола

Только суперюзер может выполнять эти команды. В будущем — через UI-интерфейс.

| Команда | Параметры |
|---|---|
| `CreateUser` | username, password |
| `DeleteUser` | username |
| `ChangePassword` | username, newPassword |
| `GrantPermission` | username, queuePattern, operations[] |
| `RevokePermission` | username, queuePattern |
| `ListUsers` | — |
| `GetUserPermissions` | username |

---

## Детальный план реализации

### Шаг 1: SQLite auth repository

**Файлы:**
- `src/VibeMQ.Server/Auth/IAuthRepository.cs`
- `src/VibeMQ.Server/Auth/SqliteAuthRepository.cs`
- `src/VibeMQ.Server/Auth/Models/UserRecord.cs`
- `src/VibeMQ.Server/Auth/Models/PermissionEntry.cs`

**Задачи:**
1. `IAuthRepository`:
   ```csharp
   Task<UserRecord?> FindUserAsync(string username, CancellationToken ct);
   Task CreateUserAsync(UserRecord user, CancellationToken ct);
   Task UpdatePasswordHashAsync(string username, string hash, CancellationToken ct);
   Task DeleteUserAsync(string username, CancellationToken ct);
   Task<IReadOnlyList<UserRecord>> ListUsersAsync(CancellationToken ct);
   Task<IReadOnlyList<PermissionEntry>> GetPermissionsAsync(string username, CancellationToken ct);
   Task GrantPermissionAsync(string username, string queuePattern, QueueOperation[] ops, CancellationToken ct);
   Task RevokePermissionAsync(string username, string queuePattern, CancellationToken ct);
   ```
2. `SqliteAuthRepository`: Microsoft.Data.Sqlite, отдельный файл `auth.db` (путь из конфига)
3. `CreateSchemaAsync()` — создать таблицы при старте если не существуют
4. `UserRecord`: `Username`, `PasswordHash`, `IsSuperuser`, `CreatedAt`, `UpdatedAt`
5. `PermissionEntry`: `QueuePattern`, `Operations` (десериализовать из JSON)

### Шаг 2: Обновить конфигурацию

**Файл:** `src/VibeMQ.Core/Configuration/BrokerOptions.cs`

**Задачи:**
1. Добавить `AuthorizationOptions Authorization { get; set; } = new()`
2. `AuthorizationOptions`: `SuperuserUsername`, `SuperuserPassword`, `DatabasePath` (default: `auth.db`)
3. Пометить `AuthToken` как `[Obsolete("Use Authorization.SuperuserPassword")]`
4. Добавить builder-метод: `BrokerBuilder.UseAuthorization(Action<AuthorizationOptions> configure)`

### Шаг 3: Bootstrap суперюзера

**Файл:** `src/VibeMQ.Server/Auth/AuthBootstrapper.cs`

**Задачи:**
1. `AuthBootstrapper.InitializeAsync(IAuthRepository, AuthorizationOptions, CancellationToken)`
2. Вызов `CreateSchemaAsync()`
3. Проверка: если суперюзер не существует → `BCrypt.HashPassword(SuperuserPassword)` → `CreateUserAsync`
4. Вызов при старте `BrokerServer` до принятия соединений

### Шаг 4: Обновить аутентификацию

**Файлы:**
- `src/VibeMQ.Core/Interfaces/IAuthenticationService.cs`
- `src/VibeMQ.Server/Auth/PasswordAuthenticationService.cs`
- `src/VibeMQ.Server/Connections/ClientConnection.cs`

**Задачи:**
1. Новый метод в `IAuthenticationService`:
   ```csharp
   Task<AuthResult?> AuthenticateAsync(string username, string password, CancellationToken ct);
   // null = провал; AuthResult содержит Username, IsSuperuser, CachedPermissions
   ```
2. `PasswordAuthenticationService`:
   - `FindUserAsync` → проверить BCrypt → загрузить permissions
   - Использует `IAuthRepository`
3. `ClientConnection`: добавить `Username`, `IsSuperuser`, `CachedPermissions`
4. Оставить старый `AuthenticateAsync(string token)` как deprecated-заглушку для тестов

### Шаг 5: Обновить ConnectHandler

**Файл:** `src/VibeMQ.Server/Handlers/ConnectHandler.cs`

**Задачи:**
1. Извлечь `username` + `password` из заголовков/тела Connect-сообщения
2. Вызвать `AuthenticateAsync(username, password)` → заполнить `connection.Username`, `.IsSuperuser`, `.CachedPermissions`
3. Обновить сообщения ошибок: `AUTH_REQUIRED` (нет полей), `AUTH_FAILED` (неверные данные)

### Шаг 6: IAuthorizationService и GlobMatcher

**Файлы:**
- `src/VibeMQ.Core/Interfaces/IAuthorizationService.cs`
- `src/VibeMQ.Core/Enums/QueueOperation.cs`
- `src/VibeMQ.Server/Auth/AuthorizationService.cs`
- `src/VibeMQ.Server/Auth/GlobMatcher.cs`

**Задачи:**
1. `IAuthorizationService`:
   ```csharp
   ValueTask<bool> IsAuthorizedAsync(
       ClientConnection connection,
       QueueOperation operation,
       string? queueName,
       CancellationToken ct);
   ```
2. `AuthorizationService`:
   - Суперюзер → всегда `true`
   - Перебор `connection.CachedPermissions`: `GlobMatcher.IsMatch(queueName, pattern)` → union операций
   - Проверить `operation` ∈ итоговом union → вернуть результат
3. `GlobMatcher.IsMatch(string input, string pattern)`:
   - `*` → `.*` в regex; `.` в паттерне экранируется
   - Компиляция с кэшированием (`ConcurrentDictionary<string, Regex>`)

### Шаг 7: Интеграция в обработчики

**Файлы:** все существующие Handler'ы

**Задачи:**
1. `PublishHandler` — проверка `Publish`
2. `SubscribeHandler` — проверка `Subscribe`
3. `CreateQueueHandler` — проверка `CreateQueue`
4. `DeleteQueueHandler` — проверка `DeleteQueue`
5. `PurgeQueueHandler` — проверка `PurgeQueue` (если реализован)
6. `GetQueueInfoHandler` — проверка `GetQueueInfo`
7. `ListQueuesHandler` — filtered: матчинг каждой очереди против паттернов пользователя
8. Добавить `ErrorCodes.NotAuthorized` константу

### Шаг 8: Admin-команды протокола

**Файлы:**
- `src/VibeMQ.Core/Protocol/AdminCommands.cs` — новые типы команд
- `src/VibeMQ.Server/Handlers/Admin/CreateUserHandler.cs`
- `src/VibeMQ.Server/Handlers/Admin/DeleteUserHandler.cs`
- `src/VibeMQ.Server/Handlers/Admin/ChangePasswordHandler.cs`
- `src/VibeMQ.Server/Handlers/Admin/GrantPermissionHandler.cs`
- `src/VibeMQ.Server/Handlers/Admin/RevokePermissionHandler.cs`
- `src/VibeMQ.Server/Handlers/Admin/ListUsersHandler.cs`
- `src/VibeMQ.Server/Handlers/Admin/GetUserPermissionsHandler.cs`

**Задачи:**
1. Все admin-обработчики: первым делом проверить `connection.IsSuperuser`, иначе `NOT_AUTHORIZED`
   — исключение: `ChangePassword` с `targetUsername == connection.Username` разрешён любому
2. `CreateUser`: валидация username (непустой, без спецсимволов), BCrypt пароля, insert в SQLite
3. `ChangePassword`: BCrypt нового пароля, update в SQLite
   - обычный пользователь: `targetUsername == connection.Username` → разрешено; иначе → `NOT_AUTHORIZED`
   - суперюзер: может менять пароль любого пользователя
4. `GrantPermission`: валидация `queuePattern` и `operations[]`, upsert в `permissions`
5. `RevokePermission`: delete из `permissions` по username + queuePattern
6. `ListUsers`: вернуть список `username, is_superuser, created_at` (без хэшей)
7. `GetUserPermissions`: вернуть список `(queuePattern, operations[])` для пользователя
8. Зарегистрировать новые handler'ы в `BrokerServer`

### Шаг 9: DI и регистрация

**Файл:** `src/VibeMQ.Server/Extensions/BrokerBuilderExtensions.cs`

**Задачи:**
1. `UseAuthorization(Action<AuthorizationOptions>)` — регистрирует `SqliteAuthRepository`,
   `PasswordAuthenticationService`, `AuthorizationService`, `AuthBootstrapper`
2. `IAuthorizationService` как `null` по умолчанию — handler'ы пропускают проверку
3. Вызов `AuthBootstrapper.InitializeAsync` при `BrokerServer.StartAsync`

### Шаг 10: Тесты

**Задачи:**

*GlobMatcher:*
1. Точное совпадение: `"orders"` матчит `"orders"` → true
2. Wildcard: `"orders.*"` матчит `"orders.new"`, `"orders.retry"` → true
3. Wildcard не матчит: `"orders.*"` vs `"payments.new"` → false
4. Универсальный: `"*"` матчит любую строку → true
5. Несколько паттернов — union: `"orders.*"→[Publish]` + `"*.dlq"→[Subscribe]` + `"orders.dlq"` → `[Publish, Subscribe]`

*Аутентификация:*
6. Верные credentials → `AuthResult` с корректным username
7. Неверный пароль → null
8. Несуществующий пользователь → null
9. Bootstrap суперюзера: пустая база → создан с хэшированным паролем
10. Повторный старт → суперюзер не пересоздаётся

*Авторизация:*
11. Разрешение есть → операция проходит
12. Разрешения нет → `NOT_AUTHORIZED`
13. Суперюзер → всегда true
14. `EnableAuthentication = false` → авторизация не проверяется (backward compat)

*ListQueues filtered:*
15. Пользователь с `"orders.*" → [Publish]`: листинг содержит `orders.new`, не содержит `payments.new`
16. Суперюзер: видит все очереди

*Admin-команды:*
17. Суперюзер → `CreateUser`, `GrantPermission` выполняются успешно
18. Обычный пользователь → `NOT_AUTHORIZED`
19. `ChangePassword` своего аккаунта → разрешено обычному пользователю
20. `ChangePassword` чужого аккаунта обычным пользователем → `NOT_AUTHORIZED`
21. `ChangePassword` чужого аккаунта суперюзером → разрешено
22. `DeleteUser` суперюзера → запрещено (защита от потери доступа)

*Производительность:*
21. Permissions кэшируются в `ClientConnection`, SQLite не вызывается при каждой операции

---

## Архитектурные решения

- **SQLite вне `IStorageProvider`**: хранилище авторизации (`auth.db`) — отдельная ответственность.
  Основной storage provider может смениться на Redis или RocksDB; auth должен работать всегда
  и иметь реляционную семантику (foreign keys, транзакции). SQLite — единственный вариант.

- **Кэш permissions per-session**: загрузка при логине, инвалидация при переподключении.
  Простота важнее real-time синхронизации для v1. Если понадобится — добавим принудительный
  disconnect при `RevokePermission`.

- **Union при нескольких паттернах**: нет концепции «deny override allow». Первая реализация
  аддитивная — проще и предсказуемее. Explicit deny можно добавить позже если появится требование.

- **Суперюзер не может удалить сам себя**: защита от lockout. `DeleteUser` проверяет
  `is_superuser = 1` целевого пользователя → отклонить с `FORBIDDEN`.

- **BCrypt cost factor**: по умолчанию 12. Аутентификация кэшируется в сессии — в hot path
  BCrypt не вызывается.
