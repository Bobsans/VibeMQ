namespace VibeMQ.Protocol.Compression;

/// <summary>
/// Identifies the compression algorithm applied to a protocol frame body.
/// </summary>
public enum CompressionAlgorithm : byte {
    /// <summary>No compression. Frame body is written as-is.</summary>
    None = 0,

    /// <summary>GZip compression via <see cref="System.IO.Compression.GZipStream"/>.</summary>
    GZip = 1,

    /// <summary>Brotli compression via <see cref="System.IO.Compression.BrotliStream"/>.</summary>
    Brotli = 2,
}
