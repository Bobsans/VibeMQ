# Гранулярная авторизация

**Описание:** Детальная система разрешений для пользователей.

**Детальный план:**

## Шаг 1: Проектирование модели разрешений
**Задачи:**
1. Определить модель:
   - Разрешения: `Read`, `Write`, `Admin` для очередей
   - Операции: `Publish`, `Subscribe`, `CreateQueue`, `DeleteQueue`
   - Роли: `Publisher`, `Subscriber`, `Admin`, `Viewer`
2. Определить формат политик (JSON или код)

## Шаг 2: Создание интерфейсов
**Файл:** `src/VibeMQ.Core/Interfaces/IAuthorizationService.cs`
**Файл:** `src/VibeMQ.Core/Interfaces/IPermissionProvider.cs`

**Задачи:**
1. Создать интерфейс `IAuthorizationService`:
   ```csharp
   Task<bool> AuthorizeAsync(string userId, string operation, string resource);
   Task<IReadOnlyList<Permission>> GetPermissionsAsync(string userId);
   ```
2. Создать интерфейс `IPermissionProvider` для получения разрешений пользователя

## Шаг 3: Реализация базовой авторизации
**Файл:** `src/VibeMQ.Server/Auth/AuthorizationService.cs`
**Файл:** `src/VibeMQ.Core/Models/Permission.cs`
**Файл:** `src/VibeMQ.Core/Models/Role.cs`

**Задачи:**
1. Создать модели `Permission`, `Role`
2. Реализовать `AuthorizationService`:
   - Проверка разрешений по ролям
   - Проверка разрешений по ресурсам (очередь)
   - Кэширование разрешений
3. Интеграция с `IAuthenticationService` для получения userId

## Шаг 4: Интеграция в обработчики
**Файл:** `src/VibeMQ.Server/Handlers/PublishHandler.cs`
**Файл:** `src/VibeMQ.Server/Handlers/SubscribeHandler.cs`
**Файл:** `src/VibeMQ.Server/Queues/QueueManager.cs`

**Задачи:**
1. Добавить проверку авторизации в `PublishHandler`
2. Добавить проверку авторизации в `SubscribeHandler`
3. Добавить проверку авторизации в `QueueManager` (CreateQueue, DeleteQueue)
4. Возврат ошибок при отсутствии разрешений

## Шаг 5: RBAC реализация
**Файл:** `src/VibeMQ.Server/Auth/RoleBasedAuthorizationService.cs`

**Задачи:**
1. Реализовать ролевую модель:
   - Определение ролей и их разрешений
   - Назначение ролей пользователям
2. Конфигурация ролей через `BrokerOptions`

## Шаг 6: Интеграция с внешними провайдерами
**Файл:** `src/VibeMQ.Server.Auth.LDAP/LdapPermissionProvider.cs`
**Файл:** `src/VibeMQ.Server.Auth.OAuth/OAuthPermissionProvider.cs`

**Задачи:**
1. Создать интерфейс для внешних провайдеров
2. Реализовать LDAP провайдер (опционально)
3. Реализовать OAuth провайдер (опционально)

## Шаг 7: Конфигурация
**Файл:** `src/VibeMQ.Core/Configuration/BrokerOptions.cs`

**Задачи:**
1. Добавить конфигурацию разрешений:
   ```csharp
   public AuthorizationOptions Authorization { get; set; } = new();
   ```
2. Формат конфигурации (JSON или fluent API)

## Шаг 8: Тестирование
**Задачи:**
1. Тесты авторизации для разных ролей
2. Тесты для разных операций
3. Тесты производительности (кэширование)

