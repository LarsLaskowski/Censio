using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace Censio.GameData.Tests;

/// <summary>
/// Tests for <see cref="PngWriter" />. A reader of PNG chunks and a CRC-32 are written in this class, independent of the code
/// under test.
/// </summary>
[TestClass]
public sealed class PngWriterTests
{
    #region Constants

    /// <summary>
    /// The length of the PNG signature in bytes.
    /// </summary>
    private const int SignatureLength = 8;

    /// <summary>
    /// The size of the length field and the type of a chunk.
    /// </summary>
    private const int ChunkFrame = 8;

    #endregion // Constants

    #region Methods

    /// <summary>
    /// The PNG holds the signature, an 8-bit RGBA header of the given size, and pixels that inflate to the image with filter 0
    /// in front of every row, alpha kept.
    /// </summary>
    /// <param name="width">The width in pixels</param>
    /// <param name="height">The height in pixels</param>
    [TestMethod]
    [TestCategory("AC1")]
    [DataRow(1, 1)]
    [DataRow(3, 2)]
    [DataRow(17, 5)]
    [DataRow(200, 150)]
    public void PngWriterWriteRoundTripsRgbaWithAlpha(int width, int height)
    {
        // Arrange
        var rgba = Pixels(width, height);

        // Act
        var png = PngWriter.Write(width, height, rgba);

        // Assert
        var chunks = ReadChunks(png);
        var header = chunks[0].Data;

        Assert.AreSequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, png.AsSpan(0, SignatureLength).ToArray(), "The signature should be the PNG signature");
        Assert.HasCount(13, header, "The header chunk should hold 13 bytes");
        Assert.AreEqual(width, BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(0)), "The header should hold the width");
        Assert.AreEqual(height, BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(4)), "The header should hold the height");
        Assert.AreSequenceEqual(new byte[] { 8, 6, 0, 0, 0 }, header.AsSpan(8).ToArray(), "The image should be 8-bit RGBA, deflate, filter method 0, not interlaced");
        Assert.AreSequenceEqual(ExpectedScanlines(width, height, rgba), Inflate(chunks), "The pixels should round-trip with filter 0 on every row");
    }

    /// <summary>
    /// The PNG holds the header, one data chunk and the end chunk, nothing else, and nothing follows the end chunk.
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void PngWriterWriteHasOnlyHeaderDataAndEndChunks()
    {
        // Arrange
        var rgba = Pixels(5, 4);

        // Act
        var png = PngWriter.Write(5, 4, rgba);

        // Assert
        var chunks = ReadChunks(png);
        var consumed = SignatureLength + chunks.Sum(chunk => ChunkFrame + chunk.Data.Length + 4);

        Assert.AreEqual("IHDR,IDAT,IEND", string.Join(',', chunks.Select(chunk => chunk.Type)), "The chunks should be the header, one data chunk and the end");
        Assert.IsEmpty(chunks[2].Data, "The end chunk should be empty");
        Assert.AreEqual(png.Length, consumed, "Nothing should follow the end chunk");
    }

    /// <summary>
    /// Every chunk carries the CRC-32 of its type and data, the end chunk the well-known value, and the header of a 2 by 3
    /// image the value computed with zlib's CRC-32.
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void PngWriterWriteChunkCrcsAreValid()
    {
        // Arrange
        var rgba = Pixels(2, 3);

        // Act
        var png = PngWriter.Write(2, 3, rgba);

        // Assert
        var chunks = ReadChunks(png);

        var invalid = chunks.Where(chunk => Crc32(Encoding.ASCII.GetBytes(chunk.Type).Concat(chunk.Data).ToArray()) != chunk.Crc).Select(chunk => chunk.Type);

        Assert.AreEqual(string.Empty, string.Join(',', invalid), "No chunk should carry a CRC that differs from its type and data");
        Assert.AreEqual(0xAE426082u, chunks[2].Crc, "The CRC of the end chunk should be AE426082");
        Assert.AreEqual(0xB9EADE81u, chunks[0].Crc, "The CRC of the header of a 2 by 3 RGBA image should be B9EADE81");
    }

    /// <summary>
    /// Gets deterministic pixels with transparent, translucent and opaque values.
    /// </summary>
    /// <param name="width">The width in pixels</param>
    /// <param name="height">The height in pixels</param>
    /// <returns>The RGBA bytes, row by row</returns>
    private static byte[] Pixels(int width, int height)
    {
        var rgba = new byte[width * height * 4];

        for (var index = 0; index < rgba.Length; index++)
        {
            rgba[index] = (byte)(index * 31 + index / 4 * 7 + 5);
        }

        for (var pixel = 0; pixel < width * height; pixel++)
        {
            rgba[pixel * 4 + 3] = AlphaOf(pixel);
        }

        return rgba;
    }

    /// <summary>
    /// Gets a transparent, opaque or translucent alpha value for a pixel.
    /// </summary>
    /// <param name="pixel">The index of the pixel</param>
    /// <returns>The alpha value</returns>
    private static byte AlphaOf(int pixel)
    {
        return (pixel % 3) switch
               {
                   0 => 0,
                   1 => 255,
                   _ => (byte)(pixel * 53)
               };
    }

    /// <summary>
    /// Gets the scanlines the data chunk must inflate to.
    /// </summary>
    /// <param name="width">The width in pixels</param>
    /// <param name="height">The height in pixels</param>
    /// <param name="rgba">The pixels</param>
    /// <returns>A filter byte of 0 in front of every row</returns>
    private static byte[] ExpectedScanlines(int width, int height, byte[] rgba)
    {
        var stride = width * 4;
        var scanlines = new byte[(stride + 1) * height];

        for (var row = 0; row < height; row++)
        {
            rgba.AsSpan(row * stride, stride).CopyTo(scanlines.AsSpan(row * (stride + 1) + 1));
        }

        return scanlines;
    }

    /// <summary>
    /// Inflates the concatenated data chunks.
    /// </summary>
    /// <param name="chunks">The chunks</param>
    /// <returns>The inflated bytes</returns>
    private static byte[] Inflate(List<(string Type, byte[] Data, uint Crc)> chunks)
    {
        var data = chunks.Where(chunk => chunk.Type == "IDAT").SelectMany(chunk => chunk.Data).ToArray();
        using var input = new MemoryStream(data);
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();

        zlib.CopyTo(output);

        return output.ToArray();
    }

    /// <summary>
    /// Reads the chunks of a PNG after checking its signature length.
    /// </summary>
    /// <param name="png">The PNG</param>
    /// <returns>The type, data and CRC of each chunk</returns>
    private static List<(string Type, byte[] Data, uint Crc)> ReadChunks(byte[] png)
    {
        var chunks = new List<(string Type, byte[] Data, uint Crc)>();
        var position = SignatureLength;

        while (position < png.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(position));
            var type = Encoding.ASCII.GetString(png, position + 4, 4);
            var data = png.AsSpan(position + ChunkFrame, length).ToArray();
            var crc = BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(position + ChunkFrame + length));

            chunks.Add((type, data, crc));
            position += ChunkFrame + length + 4;
        }

        return chunks;
    }

    /// <summary>
    /// Computes the CRC-32 of the PNG specification.
    /// </summary>
    /// <param name="bytes">The bytes</param>
    /// <returns>The CRC-32</returns>
    private static uint Crc32(byte[] bytes)
    {
        var crc = 0xFFFFFFFFu;

        foreach (var value in bytes)
        {
            crc ^= value;

            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
            }
        }

        return ~crc;
    }

    #endregion // Methods
}