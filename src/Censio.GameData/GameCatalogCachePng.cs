using System.Buffers.Binary;
using System.IO.Compression;

namespace Censio.GameData;

/// <summary>
/// Checks that the PNG data of a cached icon is exactly what <see cref="PngWriter" /> writes for its pixels.
/// </summary>
internal static class GameCatalogCachePng
{
    #region Constants

    /// <summary>
    /// The offset of the width in the header chunk: the signature, the chunk length and the chunk type.
    /// </summary>
    private const int HeaderDataOffset = 16;

    /// <summary>
    /// The offset of the length of the data chunk: the signature and the header chunk.
    /// </summary>
    private const int DataChunkOffset = 33;

    /// <summary>
    /// The offset of the zlib stream: the data chunk's length and type follow its offset.
    /// </summary>
    private const int ZlibOffset = 41;

    /// <summary>
    /// The least length of a zlib stream: a two-byte header and the four-byte checksum.
    /// </summary>
    private const int MinZlibBytes = 6;

    /// <summary>
    /// The bytes of a pixel.
    /// </summary>
    private const int PixelBytes = 4;

    /// <summary>
    /// The flag of the second zlib header byte that announces a preset dictionary.
    /// </summary>
    private const int PresetDictionaryFlag = 0x20;

    #endregion // Constants

    #region Methods

    /// <summary>
    /// Gets the number of bytes an image decodes to: one filter byte and four bytes per pixel in every row.
    /// </summary>
    /// <param name="width">The width in pixels</param>
    /// <param name="height">The height in pixels</param>
    /// <returns>The bytes, 0 when a side is not positive</returns>
    public static long DecodedBytes(int width, int height)
    {
        return width < 1 || height < 1 ? 0 : height * (1L + (PixelBytes * (long)width));
    }

    /// <summary>
    /// Checks the PNG data of an icon whose sides lie within the bounds.
    /// </summary>
    /// <param name="width">The stored width</param>
    /// <param name="height">The stored height</param>
    /// <param name="png">The PNG data</param>
    /// <returns><c>true</c> when inflating it within its bound and writing the pixels again gives the same bytes</returns>
    public static bool IsValid(int width, int height, byte[] png)
    {
        if (width < 1 || width > GameIconReader.MaxIconSide || height < 1 || height > GameIconReader.MaxIconSide || png.Length < ZlibOffset + MinZlibBytes)
        {
            return false;
        }

        var span = png.AsSpan();

        if (BinaryPrimitives.ReadInt32BigEndian(span[HeaderDataOffset..]) != width
            || BinaryPrimitives.ReadInt32BigEndian(span[(HeaderDataOffset + 4)..]) != height)
        {
            return false;
        }

        var dataLength = BinaryPrimitives.ReadInt32BigEndian(span[DataChunkOffset..]);

        if (dataLength < MinZlibBytes || dataLength > png.Length - ZlibOffset || (png[ZlibOffset + 1] & PresetDictionaryFlag) != 0)
        {
            return false;
        }

        var decoded = Inflate(png, dataLength, (int)DecodedBytes(width, height));

        return decoded is not null && IsPlain(decoded, width, height) && IsSameAsWriter(decoded, width, height, png);
    }

    /// <summary>
    /// Inflates the data chunk into a buffer of the expected size and requires that the stream ends there.
    /// </summary>
    /// <param name="png">The PNG data</param>
    /// <param name="dataLength">The length of the data chunk</param>
    /// <param name="expected">The expected size of the inflated data</param>
    /// <returns>The inflated data, or <c>null</c> when it is damaged or not of the expected size</returns>
    private static byte[]? Inflate(byte[] png, int dataLength, int expected)
    {
        var decoded = new byte[expected + 1];
        var total = 0;

        try
        {
            using var stream = new MemoryStream(png, ZlibOffset, dataLength, false);
            using var zlib = new ZLibStream(stream, CompressionMode.Decompress);

            int read;

            while (total < decoded.Length && (read = zlib.Read(decoded, total, decoded.Length - total)) > 0)
            {
                total += read;
            }
        }
        catch (InvalidDataException)
        {
            return null;
        }

        return total == expected ? decoded[..expected] : null;
    }

    /// <summary>
    /// Checks that every row of inflated data starts with the filter byte 0.
    /// </summary>
    /// <param name="decoded">The inflated data</param>
    /// <param name="width">The width</param>
    /// <param name="height">The height</param>
    /// <returns><c>true</c> when no row is filtered</returns>
    private static bool IsPlain(byte[] decoded, int width, int height)
    {
        var rowBytes = 1 + (PixelBytes * width);

        for (var row = 0; row < height; row++)
        {
            if (decoded[row * rowBytes] != 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Writes the pixels again and compares the result with the stored PNG data.
    /// </summary>
    /// <param name="decoded">The inflated data</param>
    /// <param name="width">The width</param>
    /// <param name="height">The height</param>
    /// <param name="png">The stored PNG data</param>
    /// <returns><c>true</c> when both are the same bytes</returns>
    private static bool IsSameAsWriter(byte[] decoded, int width, int height, byte[] png)
    {
        var rowPixels = PixelBytes * width;
        var pixels = new byte[rowPixels * height];

        for (var row = 0; row < height; row++)
        {
            decoded.AsSpan((row * (1 + rowPixels)) + 1, rowPixels).CopyTo(pixels.AsSpan(row * rowPixels));
        }

        return PngWriter.Write(width, height, pixels).AsSpan().SequenceEqual(png);
    }

    #endregion // Methods
}