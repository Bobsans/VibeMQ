using System.Text;
using VibeMQ.Protocol.Compression;

namespace VibeMQ.Tests.Unit.Protocol;

public class CompressionTests {
    private static readonly byte[] SmallPayload = Encoding.UTF8.GetBytes("Hello, VibeMQ!");
    private static readonly byte[] LargePayload = Encoding.UTF8.GetBytes(new string('x', 4096));

    // -------------------------------------------------------------------------
    // GZip
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GZip_CompressDecompress_RestoresOriginal() {
        var compressor = CompressorFactory.Get(CompressionAlgorithm.GZip)!;

        var compressed = await compressor.CompressAsync(SmallPayload);
        var decompressed = await compressor.DecompressAsync(compressed);

        Assert.Equal(SmallPayload, decompressed);
    }

    [Fact]
    public async Task GZip_Algorithm_IsGZip() {
        var compressor = CompressorFactory.Get(CompressionAlgorithm.GZip)!;
        Assert.Equal(CompressionAlgorithm.GZip, compressor.Algorithm);
    }

    [Fact]
    public async Task GZip_LargePayload_CompressDecompress_RestoresOriginal() {
        var compressor = CompressorFactory.Get(CompressionAlgorithm.GZip)!;

        var compressed = await compressor.CompressAsync(LargePayload);
        var decompressed = await compressor.DecompressAsync(compressed);

        Assert.Equal(LargePayload, decompressed);
        Assert.True(compressed.Length < LargePayload.Length, "GZip should reduce repetitive data.");
    }

    // -------------------------------------------------------------------------
    // Brotli
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Brotli_CompressDecompress_RestoresOriginal() {
        var compressor = CompressorFactory.Get(CompressionAlgorithm.Brotli)!;

        var compressed = await compressor.CompressAsync(SmallPayload);
        var decompressed = await compressor.DecompressAsync(compressed);

        Assert.Equal(SmallPayload, decompressed);
    }

    [Fact]
    public async Task Brotli_Algorithm_IsBrotli() {
        var compressor = CompressorFactory.Get(CompressionAlgorithm.Brotli)!;
        Assert.Equal(CompressionAlgorithm.Brotli, compressor.Algorithm);
    }

    [Fact]
    public async Task Brotli_LargePayload_CompressDecompress_RestoresOriginal() {
        var compressor = CompressorFactory.Get(CompressionAlgorithm.Brotli)!;

        var compressed = await compressor.CompressAsync(LargePayload);
        var decompressed = await compressor.DecompressAsync(compressed);

        Assert.Equal(LargePayload, decompressed);
        Assert.True(compressed.Length < LargePayload.Length, "Brotli should reduce repetitive data.");
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
