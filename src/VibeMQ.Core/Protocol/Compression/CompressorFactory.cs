namespace VibeMQ.Protocol.Compression;

/// <summary>
/// Resolves <see cref="ICompressor"/> instances by algorithm and parses/serializes algorithm names.
/// </summary>
public static class CompressorFactory {
    private static readonly GZipCompressor _gZipInstance = new();
    private static readonly BrotliCompressor _brotliInstance = new();

    /// <summary>
    /// Returns the compressor for the given algorithm, or <c>null</c> for <see cref="CompressionAlgorithm.None"/>.
    /// </summary>
    public static ICompressor? Get(CompressionAlgorithm algorithm) => algorithm switch {
        CompressionAlgorithm.GZip => _gZipInstance,
        CompressionAlgorithm.Brotli => _brotliInstance,
        _ => null
    };

    /// <summary>
    /// Parses a compression algorithm name (<c>"gzip"</c>, <c>"brotli"</c>).
    /// Returns <c>null</c> for unrecognized or empty names.
    /// </summary>
    public static CompressionAlgorithm? Parse(string? name) => name?.ToLowerInvariant() switch {
        "gzip" => CompressionAlgorithm.GZip,
        "brotli" => CompressionAlgorithm.Brotli,
        _ => null
    };

    /// <summary>
    /// Serializes a compression algorithm to its protocol wire name.
    /// </summary>
    public static string Serialize(CompressionAlgorithm algorithm) => algorithm switch {
        CompressionAlgorithm.GZip => "gzip",
        CompressionAlgorithm.Brotli => "brotli",
        _ => "none"
    };
}
