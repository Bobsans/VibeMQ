# Настройка версионирования

**Описание:** Унифицировать версионирование проектов (SemVer), синхронизировать версии между пакетами, настроить генерацию changelog.

**Детальный план:**

## Шаг 1: Централизованное управление версиями
**Файл:** `Directory.Build.props`

**Задачи:**
1. Добавить централизованную версию:
   ```xml
   <PropertyGroup>
     <Version>1.0.0</Version>
     <VersionPrefix>1.0.0</VersionPrefix>
     <VersionSuffix></VersionSuffix>
   </PropertyGroup>
   ```
2. Удалить версии из отдельных `.csproj` файлов
3. Использовать SemVer 2.0 формат

## Шаг 2: Настройка версий для NuGet пакетов
**Файл:** `Directory.Build.props`

**Задачи:**
1. Убедиться что все проекты используют одну версию
2. Настроить `PackageVersion` для NuGet пакетов
3. Настроить `AssemblyVersion` и `FileVersion`

## Шаг 3: Настройка генерации changelog
**Файл:** `.github/workflows/changelog.yml` (или аналогичный)

**Задачи:**
1. Выбрать инструмент: `git-cliff`, `conventional-changelog`, или ручной формат
2. Настроить автоматическую генерацию из git commits
3. Формат: `CHANGELOG.md` или `docs/changelog.rst`

## Шаг 4: Документация процесса версионирования
**Документ:** `docs/docs/versioning.rst`

**Задачи:**
1. Описать процесс версионирования
2. Правила для major/minor/patch версий
3. Процесс релиза

