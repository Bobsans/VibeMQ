using System.Text;
using VibeMQ.Protocol.Compression;

namespace VibeMQ.Tests.Unit.Protocol;

public class CompressionTests {
    private static readonly byte[] _smallPayload = "Hello, VibeMQ!"u8.ToArray();
    private static readonly byte[] _largePayload = Encoding.UTF8.GetBytes(new string('x', 4096));

    // -------------------------------------------------------------------------
    // GZip
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GZip_CompressDecompress_RestoresOriginal() {
        var compressor = CompressorFactory.Get(CompressionAlgorithm.GZip)!;

        var compressed = await compressor.CompressAsync(_smallPayload);
        var decompressed = await compressor.DecompressAsync(compressed);

        Assert.Equal(_smallPayload, decompressed);
    }

    [Fact]
    public Task GZip_Algorithm_IsGZip() {
        try {
            var compressor = CompressorFactory.Get(CompressionAlgorithm.GZip)!;
            Assert.Equal(CompressionAlgorithm.GZip, compressor.Algorithm);
            return Task.CompletedTask;
        } catch (Exception exception) {
            return Task.FromException(exception);
        }
    }

    [Fact]
    public async Task GZip_LargePayload_CompressDecompress_RestoresOriginal() {
        var compressor = CompressorFactory.Get(CompressionAlgorithm.GZip)!;

        var compressed = await compressor.CompressAsync(_largePayload);
        var decompressed = await compressor.DecompressAsync(compressed);

        Assert.Equal(_largePayload, decompressed);
        Assert.True(compressed.Length < _largePayload.Length, "GZip should reduce repetitive data.");
    }

    // -------------------------------------------------------------------------
    // Brotli
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Brotli_CompressDecompress_RestoresOriginal() {
        var compressor = CompressorFactory.Get(CompressionAlgorithm.Brotli)!;

        var compressed = await compressor.CompressAsync(_smallPayload);
        var decompressed = await compressor.DecompressAsync(compressed);

        Assert.Equal(_smallPayload, decompressed);
    }

    [Fact]
    public Task Brotli_Algorithm_IsBrotli() {
        try {
            var compressor = CompressorFactory.Get(CompressionAlgorithm.Brotli)!;
            Assert.Equal(CompressionAlgorithm.Brotli, compressor.Algorithm);
            return Task.CompletedTask;
        } catch (Exception exception) {
            return Task.FromException(exception);
        }
    }

    [Fact]
    public async Task Brotli_LargePayload_CompressDecompress_RestoresOriginal() {
        var compressor = CompressorFactory.Get(CompressionAlgorithm.Brotli)!;

        var compressed = await compressor.CompressAsync(_largePayload);
        var decompressed = await compressor.DecompressAsync(compressed);

        Assert.Equal(_largePayload, decompressed);
        Assert.True(compressed.Length < _largePayload.Length, "Brotli should reduce repetitive data.");
    }

    // -------------------------------------------------------------------------
    // CompressorFactory
    // -------------------------------------------------------------------------

    [Fact]
    public void Factory_Get_None_ReturnsNull() {
        Assert.Null(CompressorFactory.Get(CompressionAlgorithm.None));
    }

    [Theory]
    [InlineData("gzip", CompressionAlgorithm.GZip)]
    [InlineData("GZIP", CompressionAlgorithm.GZip)]
    [InlineData("brotli", CompressionAlgorithm.Brotli)]
    [InlineData("BROTLI", CompressionAlgorithm.Brotli)]
    public void Factory_Parse_KnownNames_ReturnsAlgorithm(string name, CompressionAlgorithm expected) {
        Assert.Equal(expected, CompressorFactory.Parse(name));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("zstd")]
    [InlineData("none")]
    public void Factory_Parse_UnknownOrEmpty_ReturnsNull(string? name) {
        Assert.Null(CompressorFactory.Parse(name));
    }

    [Theory]
    [InlineData(CompressionAlgorithm.GZip, "gzip")]
    [InlineData(CompressionAlgorithm.Brotli, "brotli")]
    [InlineData(CompressionAlgorithm.None, "none")]
    public void Factory_Serialize_ReturnsExpectedName(CompressionAlgorithm algorithm, string expected) {
        Assert.Equal(expected, CompressorFactory.Serialize(algorithm));
    }
}
