namespace VibeMQ.Protocol.Compression;

/// <summary>
/// Compresses and decompresses byte sequences for protocol frame transport.
/// </summary>
public interface ICompressor {
    /// <summary>The algorithm implemented by this compressor.</summary>
    CompressionAlgorithm Algorithm { get; }

    /// <summary>Compresses <paramref name="data"/> and returns the compressed bytes.</summary>
    ValueTask<byte[]> CompressAsync(ReadOnlyMemory<byte> data);

    /// <summary>Decompresses <paramref name="data"/> and returns the original bytes.</summary>
    ValueTask<byte[]> DecompressAsync(ReadOnlyMemory<byte> data);
}
