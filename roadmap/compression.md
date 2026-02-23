# Сжатие на уровне протокола

**Описание:** Опциональное сжатие фреймов для уменьшения трафика при передаче больших сообщений.

---

## Принятые решения

| Вопрос | Решение |
|--------|---------|
| Алгоритмы | GZip, Brotli (только встроенные .NET, без внешних зависимостей) |
| Уровень применения | Весь body фрейма целиком (после binary codec) |
| Threshold | 1 KB (1024 байт) — меньше не сжимать |
| Negotiation | Заголовки `supported-compression` / `negotiated-compression` в Connect/ConnectAck |
| Флаг сжатия | 1 байт в frame header (между length prefix и body) |

---

## Wire-формат

### До (текущий)
```
[4B: body_length uint32 BE][N bytes: binary body]
```

### После
```
[4B: body_length uint32 BE][1B: compression_flags][N bytes: binary body]
```

`compression_flags`:
- `0x00` — нет сжатия
- `0x01` — GZip
- `0x02` — Brotli

`body_length` всегда содержит длину **после** сжатия (если применялось).

> Каждый фрейм самодостаточен: флаг сжатия хранится в самом фрейме,
> поэтому FrameReader не зависит от состояния соединения.

---

## Negotiation (Connect handshake)

```
Client → Connect:
  headers["supported-compression"] = "brotli,gzip"   // по убыванию приоритета

Server → ConnectAck:
  headers["negotiated-compression"] = "brotli"        // выбранный алгоритм
  // отсутствует, если сервер не поддерживает сжатие
```

После handshake FrameWriter на обеих сторонах знает, какой алгоритм использовать.
FrameReader алгоритм узнаёт из флага каждого фрейма.

---

## Шаг 1: Инфраструктура сжатия

**Проект:** `VibeMQ.Protocol`

**Новые файлы:**
- `Compression/CompressionAlgorithm.cs` — enum: `None = 0, GZip = 1, Brotli = 2`
- `Compression/ICompressor.cs` — интерфейс:
  ```csharp
  interface ICompressor {
      CompressionAlgorithm Algorithm { get; }
      ValueTask<byte[]> CompressAsync(ReadOnlyMemory<byte> data);
      ValueTask<byte[]> DecompressAsync(ReadOnlyMemory<byte> data);
  }
  ```
- `Compression/GZipCompressor.cs` — реализация через `GZipStream`
- `Compression/BrotliCompressor.cs` — реализация через `BrotliStream`
- `Compression/CompressorFactory.cs` — статический/singleton factory:
  ```csharp
  static ICompressor? Get(CompressionAlgorithm algorithm)
  static ICompressor? Parse(string? name)       // "gzip" / "brotli"
  static string Serialize(CompressionAlgorithm) // обратно в строку
  ```

**Изменения в `ProtocolConstants.cs`:**
- Добавить `FRAME_FLAGS_SIZE = 1`
- Добавить `COMPRESSION_THRESHOLD = 1024`

---

## Шаг 2: Обновление FrameReader

**Файл:** `Framing/FrameReader.cs`

**Изменения:**
1. После чтения 4-байтного length читать 1 байт `compression_flags`
2. Читать `body_length` байт тела
3. Если `compression_flags != 0x00`:
   - Получить компрессор через `CompressorFactory.Get(algorithm)`
   - Распаковать тело
4. Десериализовать через `VibeMQBinaryCodec`

```
FrameReader не хранит состояние алгоритма — алгоритм читается из каждого фрейма.
```

---

## Шаг 3: Обновление FrameWriter

**Файл:** `Framing/FrameWriter.cs`

**Новое состояние:**
```csharp
private CompressionAlgorithm _algorithm = CompressionAlgorithm.None;
private int _threshold = ProtocolConstants.COMPRESSION_THRESHOLD;

public void SetCompression(CompressionAlgorithm algorithm, int threshold)
```

**Изменения в `WriteAsync`:**
1. Сериализовать сообщение через codec → `byte[]`
2. Если `_algorithm != None` И `bytes.Length >= _threshold`:
   - Сжать через компрессор
   - `flags = (byte)_algorithm`
3. Иначе: `flags = 0x00`, тело без сжатия
4. Записать: `[4B length][1B flags][N bytes body]`

---

## Шаг 4: Конфигурация

**Файл:** `VibeMQ.Core/Configuration/BrokerOptions.cs`
```csharp
// Какие алгоритмы сервер готов принять (в порядке предпочтения)
public IReadOnlyList<CompressionAlgorithm> SupportedCompressions { get; set; }
    = [CompressionAlgorithm.Brotli, CompressionAlgorithm.GZip];

public int CompressionThreshold { get; set; } = ProtocolConstants.COMPRESSION_THRESHOLD;
```

**Файл:** `VibeMQ.Core/Configuration/ClientOptions.cs`
```csharp
// Желаемые алгоритмы (в порядке предпочтения); пустой список = без сжатия
public IReadOnlyList<CompressionAlgorithm> PreferredCompressions { get; set; }
    = [CompressionAlgorithm.Brotli, CompressionAlgorithm.GZip];

public int CompressionThreshold { get; set; } = ProtocolConstants.COMPRESSION_THRESHOLD;
```

---

## Шаг 5: Negotiation на сервере

**Файл:** `VibeMQ.Server/Handlers/ConnectHandler.cs`

**Логика:**
1. Прочитать `headers["supported-compression"]` из Connect-сообщения
2. Разобрать список алгоритмов клиента
3. Найти первый, который есть и в `BrokerOptions.SupportedCompressions`
4. Если нашли — добавить `headers["negotiated-compression"]` в ConnectAck
5. Вызвать `connection.SetCompression(algorithm, threshold)` чтобы FrameWriter начал сжимать

**Файл:** `VibeMQ.Server/ClientConnection.cs`
- Добавить `SetCompression(CompressionAlgorithm, int threshold)` — делегирует в `FrameWriter`

---

## Шаг 6: Negotiation на клиенте

**Файл:** `VibeMQ.Client/VibeMQClient.cs`

**При Connect:**
1. Если `ClientOptions.PreferredCompressions` непустой — добавить `headers["supported-compression"]`
2. После получения ConnectAck:
   - Прочитать `headers["negotiated-compression"]`
   - Вызвать `_frameWriter.SetCompression(algorithm, threshold)`

---

## Шаг 7: Тестирование

**Проект:** `VibeMQ.Tests`

**Юнит-тесты:**
- `GZipCompressor`: compress → decompress → исходные данные
- `BrotliCompressor`: compress → decompress → исходные данные
- `FrameWriter/FrameReader` round-trip с каждым алгоритмом
- Сообщения ниже threshold не сжимаются (флаг = 0x00)
- Сообщения выше threshold сжимаются

**Интеграционные тесты:**
- Client с Brotli → Server с Brotli/GZip: negotiation выбирает Brotli
- Client с GZip → Server только с GZip: negotiation выбирает GZip
- Client без сжатия → Server с сжатием: работает без сжатия
- Публикация большого сообщения (>1KB): сжимается и доставляется корректно

---

## Затронутые файлы (итого)

| Файл | Изменение |
|------|-----------|
| `VibeMQ.Protocol/ProtocolConstants.cs` | +2 константы |
| `VibeMQ.Protocol/Compression/CompressionAlgorithm.cs` | новый |
| `VibeMQ.Protocol/Compression/ICompressor.cs` | новый |
| `VibeMQ.Protocol/Compression/GZipCompressor.cs` | новый |
| `VibeMQ.Protocol/Compression/BrotliCompressor.cs` | новый |
| `VibeMQ.Protocol/Compression/CompressorFactory.cs` | новый |
| `VibeMQ.Protocol/Framing/FrameReader.cs` | +1 байт в чтении фрейма, условная распаковка |
| `VibeMQ.Protocol/Framing/FrameWriter.cs` | +состояние алгоритма, условная запаковка |
| `VibeMQ.Core/Configuration/BrokerOptions.cs` | +2 свойства |
| `VibeMQ.Core/Configuration/ClientOptions.cs` | +2 свойства |
| `VibeMQ.Server/ClientConnection.cs` | +`SetCompression()` |
| `VibeMQ.Server/Handlers/ConnectHandler.cs` | negotiation логика |
| `VibeMQ.Client/VibeMQClient.cs` | negotiation логика |
