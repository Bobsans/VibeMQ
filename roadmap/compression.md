# Сжатие на уровне протокола

**Описание:** Опциональное сжатие сообщений для уменьшения трафика.

**Детальный план:**

## Шаг 1: Проектирование
**Задачи:**
1. Определить алгоритмы: gzip, brotli, lz4
2. Определить negotiation механизм:
   - В заголовках Connect команды
   - Список поддерживаемых алгоритмов
3. Определить когда применять сжатие:
   - Всегда или только для больших сообщений (threshold)

## Шаг 2: Расширение протокола
**Файл:** `src/VibeMQ.Protocol/ProtocolMessage.cs`
**Файл:** `src/VibeMQ.Protocol/Framing/FrameReader.cs`
**Файл:** `src/VibeMQ.Protocol/Framing/FrameWriter.cs`

**Задачи:**
1. Добавить поле `Compression` в `ProtocolMessage`
2. Добавить negotiation в Connect команду
3. Обновить `FrameWriter` для сжатия фреймов
4. Обновить `FrameReader` для распаковки фреймов

## Шаг 3: Реализация компрессоров
**Файл:** `src/VibeMQ.Protocol/Compression/ICompressor.cs`
**Файл:** `src/VibeMQ.Protocol/Compression/GzipCompressor.cs`
**Файл:** `src/VibeMQ.Protocol/Compression/BrotliCompressor.cs`
**Файл:** `src/VibeMQ.Protocol/Compression/Lz4Compressor.cs`

**Задачи:**
1. Создать интерфейс `ICompressor`
2. Реализовать компрессоры для каждого алгоритма
3. Использовать `System.IO.Compression` для gzip/brotli
4. Использовать библиотеку для lz4 (если нужна)

## Шаг 4: Интеграция
**Файл:** `src/VibeMQ.Server/Handlers/ConnectHandler.cs`
**Файл:** `src/VibeMQ.Client/VibeMQClient.cs`

**Задачи:**
1. Обработка negotiation на сервере
2. Обработка negotiation на клиенте
3. Применение сжатия при отправке сообщений
4. Распаковка при получении

## Шаг 5: Конфигурация
**Файл:** `src/VibeMQ.Core/Configuration/BrokerOptions.cs`
**Файл:** `src/VibeMQ.Core/Configuration/ClientOptions.cs`

**Задачи:**
1. Добавить опции для включения сжатия
2. Выбор алгоритма по умолчанию
3. Threshold для применения сжатия

## Шаг 6: Тестирование
**Задачи:**
1. Тесты сжатия/распаковки
2. Тесты производительности (сравнение алгоритмов)
3. Тесты совместимости

