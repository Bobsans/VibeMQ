using System.IO.Compression;

namespace VibeMQ.Protocol.Compression;

sealed class BrotliCompressor : ICompressor {
    public CompressionAlgorithm Algorithm => CompressionAlgorithm.Brotli;

    public async ValueTask<byte[]> CompressAsync(ReadOnlyMemory<byte> data) {
        using var output = new MemoryStream();

        await using (var brotli = new BrotliStream(output, CompressionLevel.Fastest, leaveOpen: true)) {
            await brotli.WriteAsync(data).ConfigureAwait(false);
        }

        return output.ToArray();
    }

    public async ValueTask<byte[]> DecompressAsync(ReadOnlyMemory<byte> data) {
        using var input = new MemoryStream(data.ToArray());
        await using var brotli = new BrotliStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();

        await DecompressGuard.CopyWithLimitAsync(brotli, output).ConfigureAwait(false);

        return output.ToArray();
    }
}
