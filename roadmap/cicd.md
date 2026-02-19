# CI/CD с тестами и публикацией на NuGet

**Описание:** Настроить pipeline для автоматической сборки, запуска тестов и публикации пакетов на NuGet.org.

**Платформа:** GitHub Actions

**Особенности:**
- Использование Nerdbank.GitVersioning для автоматического версионирования
- Multi-targeting для .NET 8.0 и .NET 10.0
- Публикация всех библиотечных проектов на NuGet.org
- Автоматическое создание GitHub Releases

**Стратегия публикации:**
- **Push в ветку `main`**: выполняется только сборка и тестирование, **публикация не выполняется**
- **Push тега в ветку `main`**: выполняется полный цикл - тесты → сборка → публикация на NuGet.org → создание GitHub Release
- Ветки `develop` нет - используется только ветка `main`

**Проекты для публикации на NuGet:**
- `VibeMQ.Core` (объединенный пакет, включает VibeMQ.Core, VibeMQ.Protocol и VibeMQ.Health)
- `VibeMQ.Client`
- `VibeMQ.Server`
- `VibeMQ.Client.DependencyInjection`
- `VibeMQ.Server.DependencyInjection`

**Примечание:** Проекты `VibeMQ.Core`, `VibeMQ.Protocol` и `VibeMQ.Health` будут объединены в один пакет `VibeMQ.Core` для упрощения зависимостей и уменьшения количества пакетов при публикации.

## Шаг 0: Объединение проектов (предварительный шаг)

**Задачи:**
1. Объединить проекты `VibeMQ.Protocol` и `VibeMQ.Health` в проект `VibeMQ.Core`
2. Настроить структуру проекта `VibeMQ.Core`:
   - Переместить код из `VibeMQ.Protocol` в `VibeMQ.Core`
   - Переместить код из `VibeMQ.Health` в `VibeMQ.Core`
   - Обновить namespace'ы при необходимости
   - Обновить все ссылки на проекты в других частях решения
3. Обновить зависимости:
   - `VibeMQ.Client` должен ссылаться только на `VibeMQ.Core`
   - `VibeMQ.Server` должен ссылаться только на `VibeMQ.Core`
   - Все остальные проекты должны использовать обновленные ссылки
4. Настроить публикацию пакета:
   - Убедиться, что `VibeMQ.Core.csproj` настроен для публикации (`IsPackable` не установлен в `false`)
   - Обновить описание пакета в `.csproj` файле, чтобы отразить объединение
5. Обновить тесты:
   - Убедиться, что все тесты работают с новой структурой
   - Обновить ссылки в тестовых проектах

**Результат:** После объединения будет 5 пакетов вместо 7, что упростит управление зависимостями для пользователей библиотеки.

## Шаг 1: Настройка сборки и тестов (CI)
**Файл:** `.github/workflows/ci.yml`

**Задачи:**
1. Создать workflow для:
   - Восстановления зависимостей
   - Сборки всех проектов
   - Запуска unit тестов (`VibeMQ.Tests.Unit`)
   - Запуска integration тестов (`VibeMQ.Tests.Integration`)
   - Сборки покрытия кода (опционально)
2. Триггеры:
   - Push в ветку `main` (только тесты и сборка, **без публикации**)
   - Pull requests в `main`
3. Кэширование NuGet пакетов для ускорения сборки
4. Публикация артефактов сборки (для отладки)

**Особенности:**
- Использование `actions/setup-dotnet@v4` с `dotnet-version: '10.0.x'` для установки .NET SDK 10 в CI и release workflow
- Проекты уже настроены на multi-targeting через `TargetFrameworks` в `Directory.Build.props` (.NET 8.0 и .NET 10.0)
- Пакеты будут собираться автоматически для всех указанных target frameworks без необходимости явного указания версий
- Использование `Nerdbank.GitVersioning` для автоматического версионирования
- Кэширование `~/.nuget/packages` для ускорения восстановления пакетов
- Публикация результатов тестов как артефакты
- **Важно:** При пуше в `main` выполняется только сборка и тестирование, публикация пакетов не выполняется

## Шаг 2: Настройка публикации версий
**Файл:** `.github/workflows/release.yml`

**Задачи:**
1. Создать workflow для публикации версий на NuGet.org
2. Триггеры:
   - **Push тега формата `v{version}` в ветку `main`** (например, `v1.0.0`, `v1.2.3`)
   - Тег должен соответствовать SemVer
   - Ручной запуск через `workflow_dispatch` с возможностью указать тег (опционально)
3. Версионирование:
   - Извлечение версии из тега (Nerdbank.GitVersioning автоматически определит)
   - Проверка формата версии
4. Сборка всех проектов:
   - Запуск тестов (unit и integration)
   - Сборка автоматически выполнится для всех target frameworks, указанных в `Directory.Build.props` (.NET 8.0, .NET 10.0)
   - Создание NuGet пакетов (пакеты будут содержать сборки для всех target frameworks)
   - Подписание пакетов (если настроено)
5. Публикация на NuGet.org:
   - Публикация всех пакетов только после успешного прохождения тестов
   - Использование API key из secrets
   - Использование `dotnet nuget push` с `--skip-duplicate`
   - Публикация символов (если настроено)
6. Создание GitHub Release:
   - Автоматическое создание release на основе тега
   - Генерация changelog из коммитов (опционально)
   - Прикрепление артефактов (если нужно)

**Особенности:**
- **Публикация происходит только при создании тега в `main`**
- Публикация символов на SymbolSource или NuGet.org
- Создание release notes из git commits или CHANGELOG.md
- Workflow выполнит полный цикл: тесты → сборка → публикация

## Шаг 3: Настройка секретов и переменных в GitHub

### Необходимые Secrets (Settings → Secrets and variables → Actions → Secrets)

1. **NUGET_API_KEY** (Required)
   - Описание: API ключ для публикации пакетов на NuGet.org
   - Как получить:
     1. Зайти на https://www.nuget.org/account/apikeys
     2. Войти в аккаунт NuGet.org
     3. Создать новый API key:
        - Key name: `VibeMQ GitHub Actions`
        - Expires: выбрать срок действия (рекомендуется 1-2 года)
        - Scopes: выбрать `Push new packages and package versions`
        - Glob pattern: `VibeMQ.*` (или оставить `*` для всех пакетов)
     4. Скопировать сгенерированный ключ (он показывается только один раз!)
     5. Добавить в GitHub Secrets как `NUGET_API_KEY`

### Переменные окружения (Settings → Secrets and variables → Actions → Variables)

1. **NUGET_SOURCE** (Optional, можно задать в workflow)
   - Значение: `https://api.nuget.org/v3/index.json`
   - Описание: URL источника NuGet для публикации

**Примечание:** В workflow явно задаётся `dotnet-version: '10.0.x'`. Переменная `DOTNET_VERSION` в репозитории не используется.

### Дополнительные настройки репозитория

1. **Branch protection rules** (Settings → Branches):
   - Для ветки `main`:
     - Require pull request reviews before merging
     - Require status checks to pass before merging
     - Require branches to be up to date before merging
     - Require CI workflow to pass

2. **Actions permissions** (Settings → Actions → General):
   - Workflow permissions: `Read and write permissions`
   - Allow GitHub Actions to create and approve pull requests: включить (если нужно)

## Шаг 4: Структура workflow файлов

### `.github/workflows/ci.yml`
- Триггеры: 
  - `push` в ветку `main` (только тесты и сборка, **без публикации**)
  - `pull_request` в ветку `main`
- Шаги: setup .NET SDK → restore → build → test → publish artifacts
- **Примечание:** 
  - Не требуется матрица версий .NET, так как проекты уже настроены на multi-targeting через `TargetFrameworks`
  - При пуше в `main` выполняется только сборка и тестирование, публикация не выполняется

### `.github/workflows/release.yml`
- Триггеры: 
  - `push` тега формата `v*` в ветку `main` (например, `v1.0.0`)
  - `workflow_dispatch` для ручного запуска (опционально)
- Шаги: setup .NET SDK → restore → build → test → pack → push to NuGet → create GitHub Release
- **Примечание:** 
  - Пакеты будут собраны автоматически для всех target frameworks из проекта
  - Публикация происходит только при создании тега в `main`
  - Workflow выполнит полный цикл: тесты → сборка → публикация

## Шаг 3: Тестирование pipeline

**Порядок тестирования:**

1. **Тестирование CI workflow:**
   - Создать тестовый PR в `main`
   - Убедиться, что workflow запускается
   - Проверить, что сборка проходит успешно
   - Проверить, что тесты запускаются и проходят
   - Убедиться, что пакеты **не публикуются** при пуше в `main`

2. **Тестирование публикации release:**
   - Создать тег `v1.0.0-test` на ветке `main` и запушить его
   - Убедиться, что release workflow запускается
   - Проверить, что тесты проходят
   - Проверить, что пакеты собираются
   - Проверить, что пакеты публикуются на NuGet.org (можно использовать test feed сначала)
   - Проверить версию пакета (должна соответствовать тегу)
   - Проверить создание GitHub Release

**Рекомендации:**
- Использовать test NuGet feed для первых тестов
- Проверять версии пакетов перед публикацией
- Использовать `--skip-duplicate` для предотвращения ошибок при повторных запусках
- Убедиться, что при обычном пуше в `main` публикация не происходит

## Шаг 7: Дополнительные улучшения (опционально)

1. **Публикация символов:**
   - Настроить SourceLink для отладки
   - Публиковать символы на SymbolSource или NuGet.org

2. **Code coverage:**
   - Настроить сборку покрытия кода
   - Публиковать отчеты в Codecov или Coveralls

3. **Автоматический changelog:**
   - Использовать инструменты для генерации changelog из коммитов
   - Интегрировать в процесс создания release

4. **Уведомления:**
   - Настроить уведомления в Slack/Discord при успешной публикации
   - Настроить уведомления об ошибках сборки

5. **Dependency updates:**
   - Использовать Dependabot для автоматического обновления зависимостей
   - Настроить автоматическое создание PR для обновлений

## Инструкция по настройке репозитория

**Важно:** Перед настройкой CI/CD необходимо выполнить объединение проектов (Шаг 0), чтобы структура пакетов соответствовала финальной конфигурации.

### 0. Предварительный шаг: Объединение проектов

1. Объединить `VibeMQ.Protocol` и `VibeMQ.Health` в `VibeMQ.Core`
2. Обновить все ссылки на проекты в решении
3. Убедиться, что все тесты проходят
4. Проверить, что сборка работает корректно

### 1. Настройка Secrets

1. Перейти в репозиторий на GitHub
2. Открыть **Settings** → **Secrets and variables** → **Actions**
3. Нажать **New repository secret**
4. Добавить секрет:
   - **Name:** `NUGET_API_KEY`
   - **Secret:** вставить API ключ из NuGet.org
5. Нажать **Add secret**

### 2. Проверка версионирования

1. Убедиться, что файл `version.json` существует в корне репозитория
2. Убедиться, что `Directory.Build.props` содержит ссылку на `Nerdbank.GitVersioning`
3. Проверить версию локально:
   ```bash
   dotnet tool install -g nbgv
   nbgv get-version
   ```

### 3. Создание workflow файлов

1. Создать директорию `.github/workflows/` (если не существует)
2. Создать файлы:
   - `.github/workflows/ci.yml` (тесты и сборка при пуше в main)
   - `.github/workflows/release.yml` (публикация при создании тега в main)
3. Закоммитить и запушить файлы
4. Проверить, что workflows появились в разделе **Actions**

### 4. Тестирование

1. **Тестирование CI workflow:**
   - Создать тестовый коммит и push в `main`
   - Проверить запуск CI workflow в разделе **Actions**
   - Убедиться, что выполняется сборка и тесты, но **не выполняется публикация**

2. **Тестирование release workflow:**
   - Создать тег `v1.0.0-test` на ветке `main` и запушить его: `git tag v1.0.0-test && git push origin v1.0.0-test`
   - Проверить запуск release workflow в разделе **Actions**
   - После успешной публикации проверить пакеты на NuGet.org

## Примеры команд для проверки

```bash
# Проверка версии локально
nbgv get-version

# Сборка проекта
dotnet build

# Запуск тестов
dotnet test

# Создание пакетов
dotnet pack --configuration Release

# Проверка созданных пакетов
ls -R **/bin/Release/*.nupkg

# Локальная проверка публикации (dry-run)
dotnet nuget push **/bin/Release/*.nupkg --api-key YOUR_KEY --source https://api.nuget.org/v3/index.json --skip-duplicate
```

## Известные исправления при сборке в CI

- **CA1873 (expensive logging arguments):** На hosted runner используется .NET SDK 10.x с включённым правилом CA1873. В CI логирование для этого правила **глобально отключено**: в [`Directory.Build.props`](../Directory.Build.props) при сборке в GitHub Actions (`GITHUB_ACTIONS=true`) в `NoWarn` добавляется CA1873, чтобы сборка и тесты проходили без ошибок. Локально правило остаётся включённым. Для кода: вызовы source-generated логгера с «дорогими» аргументами (например, `connection.RemoteEndPoint?.ToString() ?? "unknown"`) рекомендуется оборачивать в проверку `if (_logger.IsEnabled(LogLevel.Information)) { ... }`. Исправлено в `ConnectionManager.cs` и `VibeMQClient.cs`.
