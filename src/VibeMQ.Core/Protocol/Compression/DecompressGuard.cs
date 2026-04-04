namespace VibeMQ.Protocol.Compression;

/// <summary>
/// Protects against decompression bombs by enforcing a maximum decompressed size.
/// </summary>
static class DecompressGuard {
    /// <summary>
    /// Default maximum decompressed payload size (64 MB).
    /// </summary>
    private const int DEFAULT_MAX_DECOMPRESSED_SIZE = 64 * 1024 * 1024;

    /// <summary>
    /// Copies from <paramref name="source"/> to <paramref name="destination"/> with a size limit.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the decompressed data exceeds the limit.</exception>
    public static async Task CopyWithLimitAsync(
        Stream source,
        MemoryStream destination,
        int maxSize = DEFAULT_MAX_DECOMPRESSED_SIZE
    ) {
        var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(81920);
        try {
            long totalRead = 0;

            int bytesRead;
            while ((bytesRead = await source.ReadAsync(buffer).ConfigureAwait(false)) > 0) {
                totalRead += bytesRead;

                if (totalRead > maxSize) {
                    // Reset the destination so callers cannot observe partial data
                    destination.SetLength(0);
                    throw new InvalidOperationException(
                        $"Decompressed payload exceeds maximum allowed size ({maxSize} bytes). Possible decompression bomb.");
                }

                await destination.WriteAsync(buffer.AsMemory(0, bytesRead)).ConfigureAwait(false);
            }
        } finally {
            System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
