# Декларативное создание очередей при подключении клиента

**Описание:** Возможность задать список очередей прямо в конфигурации клиента, чтобы они
автоматически создавались с нужными настройками при подключении. Аналогично AMQP-концепции
«declaration», но без лишней церемонии: объявил — клиент сам разобрался.

---

## Текущее состояние

- `IVibeMQClient.CreateQueueAsync` существует, но вызывается вручную после подключения.
- `ClientOptions` содержит только транспортные настройки (таймауты, TLS, реконнект).
- `QueueInfo`, возвращаемый `GetQueueInfoAsync`, содержит лишь часть полей `QueueOptions`
  (`DeliveryMode`, `MaxSize`) — полное сравнение настроек сейчас невозможно.
- Серверный `QueueManager.CreateQueueAsync` идемпотентен: если очередь уже есть — молча
  игнорирует запрос (TryAdd), без информации о конфликте.

---

## Классификация параметров по степени конфликтности

Не все расхождения в настройках одинаково опасны. Система использует три уровня:

| Уровень | Поведение при расхождении |
|---|---|
| ✅ **Info** | Разница есть, но конфликта нет. Лог на Debug. `OnConflict` не триггерится |
| ⚠️ **Soft** | Поведенческое расхождение. Применяется `OnConflict`. Лог на Warning |
| 🔴 **Hard** | Семантически Breaking change. Применяется `OnConflict`. Лог на Error |

### Классификация каждого параметра

| Параметр | Направление | Уровень | Причина |
|---|---|---|---|
| `Mode` | любое | 🔴 Hard | In-flight ACK-трекинг и семантика подписчиков жёстко привязаны к режиму |
| `MaxQueueSize` | увеличение | ✅ Info | Чистое расширение ёмкости, нет побочных эффектов |
| `MaxQueueSize` | уменьшение | ✅ Info | Очередь не триммит себя; overflow strategy сработает на следующем publish |
| `MessageTtl` | `null` → значение | ⚠️ Soft | Уже лежащие сообщения могут немедленно истечь |
| `MessageTtl` | значение → `null` | ✅ Info | Сообщения живут дольше, аддитивное изменение |
| `MessageTtl` | уменьшение | ⚠️ Soft | Существующие сообщения могут истечь раньше ожидаемого |
| `MessageTtl` | увеличение | ✅ Info | Сообщения живут дольше, аддитивное изменение |
| `EnableDeadLetterQueue` | `false` → `true` | ✅ Info | Аддитивное: у ошибочных сообщений появляется destination |
| `EnableDeadLetterQueue` | `true` → `false` | ⚠️ Soft | In-flight ретраи будут дропнуты вместо DLQ; инвалидирует `RedirectToDlq` |
| `DeadLetterQueueName` | любое | 🔴 Hard | Существующий DLQ «осиротеет» с данными; ретраи пойдут в другую очередь |
| `OverflowStrategy` | любое → не-`RedirectToDlq` | ✅ Info | Чисто политическое изменение, вступает в силу только при следующем overflow |
| `OverflowStrategy` | любое → `RedirectToDlq`, DLQ включён | ✅ Info | DLQ есть, перенаправлять есть куда |
| `OverflowStrategy` | любое → `RedirectToDlq`, DLQ **не включён** | 🔴 Hard | Кросс-параметрный конфликт: при overflow runtime-ошибка |
| `MaxRetryAttempts` | любое | ✅ Info | Лимит запекается в сообщение при публикации; влияет только на будущие сообщения |

---

## Поведение `OnConflict` по уровням

```
Уровень расхождения:   ✅ Info          ⚠️ Soft             🔴 Hard
───────────────────────────────────────────────────────────────────────
OnConflict = Ignore    Debug-лог        Warning-лог          Error-лог
                       (не конфликт)    (продолжаем)         (продолжаем)
OnConflict = Fail      Debug-лог        QueueConflictException QueueConflictException
                       (не конфликт)
OnConflict = Override  Debug-лог        ⚠️ log + Delete      ⚠️ log + Delete
                       (не конфликт)    + Recreate           + Recreate
```

**Важно:** `OnConflict` триггерится только если есть хотя бы один ⚠️ или 🔴 diff.
✅-расхождения никогда не считаются конфликтом и не влияют на поведение.

---

## Дизайн API

### Серьёзность расхождения — `ConflictSeverity`

```csharp
// src/VibeMQ.Client/ConflictSeverity.cs
public enum ConflictSeverity
{
    /// <summary>
    /// Информационное расхождение. Не является конфликтом.
    /// Логируется на Debug, не влияет на OnConflict.
    /// </summary>
    Info,

    /// <summary>
    /// Поведенческое расхождение. Является конфликтом.
    /// Логируется на Warning, применяется OnConflict.
    /// </summary>
    Soft,

    /// <summary>
    /// Семантически breaking изменение. Является конфликтом.
    /// Логируется на Error, применяется OnConflict.
    /// </summary>
    Hard,
}
```

### Стратегия разрешения конфликтов — `QueueConflictResolution`

```csharp
// src/VibeMQ.Client/QueueConflictResolution.cs
public enum QueueConflictResolution
{
    /// <summary>
    /// Оставить существующую очередь как есть.
    /// ✅-расхождения: Debug. ⚠️-расхождения: Warning. 🔴-расхождения: Error.
    /// Безопасный режим по умолчанию.
    /// </summary>
    Ignore,

    /// <summary>
    /// Бросить QueueConflictException если есть хотя бы один ⚠️ или 🔴 diff.
    /// Подходит для production: дрейф настроек = ошибка деплоя.
    /// </summary>
    Fail,

    /// <summary>
    /// Удалить очередь и создать заново при наличии ⚠️ или 🔴 diff.
    /// ⚠️ Все сообщения в очереди будут безвозвратно уничтожены.
    /// </summary>
    Override,
}
```

### Описание расхождения — `QueueSettingDiff`

```csharp
// src/VibeMQ.Client/QueueSettingDiff.cs
public sealed record QueueSettingDiff(
    string SettingName,
    object? ExistingValue,
    object? DeclaredValue,
    ConflictSeverity Severity
);
```

### Исключение при конфликте — `QueueConflictException`

```csharp
// src/VibeMQ.Client/Exceptions/QueueConflictException.cs
public sealed class QueueConflictException : Exception
{
    public string QueueName { get; }

    /// <summary>Только ⚠️ Soft и 🔴 Hard расхождения. ✅ Info сюда не попадают.</summary>
    public IReadOnlyList<QueueSettingDiff> Conflicts { get; }

    /// <summary>Максимальная серьёзность среди всех конфликтов.</summary>
    public ConflictSeverity HighestSeverity { get; }
}
```

Пример сообщения:
```
Queue 'orders' has conflicting settings [Hard, Soft]:
  [Hard] Mode:          RoundRobin  →  FanOutWithAck  (declared)
  [Soft] MessageTtl:    null        →  01:00:00        (declared)
```

### Описание одной очереди — `QueueDeclaration`

```csharp
// src/VibeMQ.Client/QueueDeclaration.cs
public sealed class QueueDeclaration
{
    /// <summary>Имя очереди.</summary>
    public required string QueueName { get; init; }

    /// <summary>Желаемые настройки очереди.</summary>
    public QueueOptions Options { get; init; } = new();

    /// <summary>
    /// Что делать при наличии ⚠️ или 🔴 расхождений.
    /// ✅ расхождения никогда не являются конфликтом.
    /// По умолчанию: Ignore.
    /// </summary>
    public QueueConflictResolution OnConflict { get; init; } = QueueConflictResolution.Ignore;

    /// <summary>
    /// Если true (по умолчанию), ошибка провижионинга прерывает ConnectAsync.
    /// Если false — логируется и пропускается.
    /// Не влияет на конфликты (ими управляет OnConflict).
    /// </summary>
    public bool FailOnProvisioningError { get; init; } = true;
}
```

### Расширение `ClientOptions`

```csharp
// src/VibeMQ.Client/ClientOptions.cs
public sealed class ClientOptions
{
    // ... существующие поля ...

    /// <summary>
    /// Очереди, которые клиент автоматически создаёт/проверяет при подключении.
    /// </summary>
    public IList<QueueDeclaration> QueueDeclarations { get; set; } = [];

    /// <summary>
    /// Добавляет декларацию очереди для автоматического провижионинга.
    /// </summary>
    public ClientOptions DeclareQueue(
        string name,
        Action<QueueOptions>? configure = null,
        QueueConflictResolution onConflict = QueueConflictResolution.Ignore,
        bool failOnError = true)
    {
        var opts = new QueueOptions();
        configure?.Invoke(opts);
        QueueDeclarations.Add(new QueueDeclaration
        {
            QueueName = name,
            Options = opts,
            OnConflict = onConflict,
            FailOnProvisioningError = failOnError,
        });
        return this;
    }
}
```

---

## Примеры использования

### Прямое использование

```csharp
var client = await VibeMQClient.ConnectAsync("localhost", 5672, options =>
{
    // В production: любое ⚠️/🔴 расхождение — ошибка деплоя
    options.DeclareQueue("orders", q =>
    {
        q.Mode = DeliveryMode.FanOutWithAck;   // 🔴 Hard если отличается
        q.MaxQueueSize = 50_000;               // ✅ Info — никогда не конфликт
        q.EnableDeadLetterQueue = true;        // ✅ Info (false→true)
        q.MessageTtl = TimeSpan.FromHours(1);  // ⚠️ Soft если null→значение
    }, onConflict: QueueConflictResolution.Fail);

    // Аналитика: расхождения не критичны
    options.DeclareQueue("analytics-events", q =>
    {
        q.MaxQueueSize = 200_000;
        q.MessageTtl = TimeSpan.FromHours(24);
        q.OverflowStrategy = OverflowStrategy.DropOldest;  // ✅ Info
    }, onConflict: QueueConflictResolution.Ignore);

    // Dev/тесты: всегда пересоздавать при любом ⚠️/🔴 расхождении
    options.DeclareQueue("transient-tasks",
        onConflict: QueueConflictResolution.Override);
});
```

### DI-интеграция

```csharp
services.AddVibeMQClient(settings =>
{
    settings.Host = "localhost";
    settings.Port = 5672;

    settings.ClientOptions.DeclareQueue("orders", q =>
    {
        q.Mode = DeliveryMode.FanOutWithAck;
        q.EnableDeadLetterQueue = true;
    }, onConflict: QueueConflictResolution.Fail);

    // failOnError: false — если не удалось создать, не ломать старт приложения
    settings.ClientOptions.DeclareQueue("notifications",
        onConflict: QueueConflictResolution.Ignore,
        failOnError: false);
});
```

---

## Алгоритм провижионинга

### Шаг 0: Предварительная валидация деклараций (до подключения)

```
Для каждого QueueDeclaration:
  ├─ OverflowStrategy == RedirectToDlq && !EnableDeadLetterQueue
  │   → InvalidOperationException (некорректная декларация, кросс-параметрный конфликт)
  └─ OK → продолжаем
```

Это ловится до подключения, при конфигурации клиента — не нужно ждать рантайма.

### Шаг 1: Провижионинг каждой очереди

Очереди обрабатываются **строго последовательно** — это гарантирует корректный порядок
создания при зависимостях между очередями (например, DLQ должна существовать до основной
очереди, если обе объявлены) и даёт предсказуемую диагностику ошибок.

На каждую операцию (`GetQueueInfoAsync`, `CreateQueueAsync`, `DeleteQueueAsync`) применяется
стандартный `ClientOptions.CommandTimeout` — отдельного таймаута провижионинга нет.

```
для каждой декларации (последовательно):

GetQueueInfoAsync(name)
├─ null → CreateQueueAsync(name, options) → ✅ готово
└─ exists →
    DiffOptions(declared, existing) → List<QueueSettingDiff>

    Разделить на:
      infoOnly  = diffs where Severity == Info   → Debug-лог, игнорируем
      conflicts = diffs where Severity >= Soft   → применяем OnConflict

    if conflicts is empty:
        → ✅ идемпотентно, ничего не делаем

    else:
        maxSeverity = conflicts.Max(d => d.Severity)

        OnConflict = Ignore:
            maxSeverity == Soft → log.Warning(формат diff)
            maxSeverity == Hard → log.Error(формат diff)
            → ✅ продолжаем

        OnConflict = Fail:
            → throw QueueConflictException(name, conflicts)

        OnConflict = Override:
            maxSeverity == Hard → log.Warning("⚠️ Deleting queue '{name}': Hard conflict, all messages will be lost")
            maxSeverity == Soft → log.Warning("⚠️ Deleting queue '{name}': Soft conflict, all messages will be lost")
            → DeleteQueueAsync(name)
            → CreateQueueAsync(name, options) → ✅ готово
```

### Шаг 2: Обработка ошибок провижионинга (сеть, права и т.д.)

```
catch (Exception ex):
    FailOnProvisioningError = true  → throw (ConnectAsync падает)
    FailOnProvisioningError = false → log.Error(...), переходим к следующей очереди
```

### Поведение при реконнекте

Провижионинг перезапускается, но:
- Если очередь не найдена (`null`) → создаётся с декларированными настройками
- Если найдена → `OnConflict` принудительно заменяется на `Ignore`

Логика: после реконнекта нужно убедиться, что очереди существуют (сервер мог перезапуститься
потеряв in-memory очереди), но не ломать реконнект из-за конфликта.

---

## Детальный план реализации

### Шаг 1: Расширить `QueueInfo` в Core

**Файл:** `src/VibeMQ.Core/Models/QueueInfo.cs`

**Задачи:**
1. Добавить недостающие поля из `QueueOptions`:
   `MessageTtl`, `EnableDeadLetterQueue`, `DeadLetterQueueName`, `OverflowStrategy`, `MaxRetryAttempts`
2. Обновить `GetQueueInfoHandler` на сервере — заполнять новые поля из `MessageQueue`
3. Обновить сериализацию протокола — новые поля в `GetQueueInfoResponse`

### Шаг 2: Новые типы в `VibeMQ.Client`

**Файлы:**
- `src/VibeMQ.Client/ConflictSeverity.cs` — enum (Info / Soft / Hard)
- `src/VibeMQ.Client/QueueConflictResolution.cs` — enum (Ignore / Fail / Override)
- `src/VibeMQ.Client/QueueSettingDiff.cs` — record (SettingName, ExistingValue, DeclaredValue, Severity)
- `src/VibeMQ.Client/QueueDeclaration.cs` — sealed class
- `src/VibeMQ.Client/Exceptions/QueueConflictException.cs` — с `Conflicts` и `HighestSeverity`

### Шаг 3: Логика классификации — `QueueSettingDiffAnalyzer`

**Файл:** `src/VibeMQ.Client/QueueSettingDiffAnalyzer.cs`

**Задачи:**
1. Реализовать статический метод `Analyze(QueueOptions declared, QueueInfo existing) → IReadOnlyList<QueueSettingDiff>`
2. Для каждого параметра — своя логика определения `ConflictSeverity` (включая direction-aware проверки)
3. Кросс-параметрная проверка `OverflowStrategy = RedirectToDlq` + `EnableDeadLetterQueue`
4. Проверка `DeadLetterQueueName` только если `EnableDeadLetterQueue = true` с обеих сторон

### Шаг 4: Расширить `ClientOptions`

**Файл:** `src/VibeMQ.Client/ClientOptions.cs`

**Задачи:**
1. Добавить `IList<QueueDeclaration> QueueDeclarations`
2. Добавить fluent-метод `DeclareQueue(name, configure?, onConflict, failOnError)`
3. Добавить метод `ValidateDeclarations()` для pre-flight проверки

### Шаг 5: Логика провижионинга в `VibeMQClient`

**Файл:** `src/VibeMQ.Client/VibeMQClient.cs`

**Задачи:**
1. Вызывать `ValidateDeclarations()` при старте `ConnectAsync` (до TCP-соединения)
2. Реализовать `ProvisionQueuesAsync(IReadOnlyList<QueueDeclaration>, bool isReconnect, CancellationToken)`
3. Вызывать `ProvisionQueuesAsync` в `ConnectAsync` после установки соединения
4. Вызывать `ProvisionQueuesAsync(isReconnect: true)` в цикле реконнекта

### Шаг 6: Обновить DI

**Файл:** `src/VibeMQ.Client.DependencyInjection/VibeMQClientSettings.cs`

**Задачи:**
1. Убедиться, что `ClientOptions` (с декларациями) доступен через `VibeMQClientSettings`
2. Проверить, что `ManagedVibeMQClient` корректно передаёт декларации при реконнекте

### Шаг 7: Тесты

**Задачи:**
1. `QueueSettingDiffAnalyzer`: unit-тест для каждого параметра × каждого направления изменения
2. Создание несуществующей очереди → должна появиться
3. Идемпотентность (декларация == существующие настройки) → no-op
4. ✅ Info-расхождения → не считаются конфликтом, Debug-лог, клиент подключается
5. ⚠️ Soft + `Ignore` → Warning-лог, клиент подключается
6. 🔴 Hard + `Ignore` → Error-лог, клиент подключается
7. ⚠️/🔴 + `Fail` → `QueueConflictException` с корректным `Conflicts` и `HighestSeverity`
8. ⚠️/🔴 + `Override` → очередь пересоздана, данные потеряны
9. Реконнект: очередь пропала → воссоздаётся; очередь с конфликтом → `Ignore` (не падает)
10. `FailOnProvisioningError = false` → ошибка залогирована, клиент подключился
11. Pre-flight валидация: `RedirectToDlq` без DLQ → `InvalidOperationException` до коннекта
12. Обновить docs: `getting-started.md`, `client-usage.md`, `configuration.md`

---

## Принятые решения по архитектуре

- **Порядок провижионинга — последовательный.** Гарантирует корректный порядок при
  зависимостях между очередями (DLQ до основной), упрощает диагностику и семантику
  `FailOnProvisioningError`. Типичное число очередей мало — разница в скорости незаметна.

- **DLQ не декларируется автоматически.** Сервер создаёт DLQ сам при первом упавшем
  сообщении (`QueueDefaults.EnableAutoCreate = true`). Автоматическая декларация породила бы
  вопрос «с какими настройками?» без однозначного ответа. Если пользователь явно хочет
  контролировать DLQ — добавляет отдельный `DeclareQueue("my.dlq")` в конфигурацию.
  Исключение: при `EnableAutoCreate = false` на сервере DLQ нужно объявлять вручную — это
  ожидаемое поведение при таком режиме.

- **Таймаут — `CommandTimeout` на каждую операцию.** Отдельный `ProvisioningTimeout` не
  добавляется: общее время ограничено `N_очередей × 2 × CommandTimeout`, что предсказуемо.
  Внешний `CancellationToken` из `ConnectAsync` и так отменяет весь провижионинг.
