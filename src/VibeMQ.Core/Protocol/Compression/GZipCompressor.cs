using System.IO.Compression;

namespace VibeMQ.Protocol.Compression;

internal sealed class GZipCompressor : ICompressor {
    public CompressionAlgorithm Algorithm => CompressionAlgorithm.GZip;

    public async ValueTask<byte[]> CompressAsync(ReadOnlyMemory<byte> data) {
        using var output = new MemoryStream();

        await using (var gzip = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true)) {
            await gzip.WriteAsync(data).ConfigureAwait(false);
        }

        return output.ToArray();
    }

    public async ValueTask<byte[]> DecompressAsync(ReadOnlyMemory<byte> data) {
        using var input = new MemoryStream(data.ToArray());
        await using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();

        await gzip.CopyToAsync(output).ConfigureAwait(false);

        return output.ToArray();
    }
}
