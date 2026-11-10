using System.Buffers.Binary;
using System.IO.Compression;

namespace Censio.GameData;

/// <summary>
/// Encodes RGBA pixels as a PNG file.
/// </summary>
public static class PngWriter
{
    #region Constants

    /// <summary>
    /// The bytes of a pixel.
    /// </summary>
    private const int PixelBytes = 4;

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The eight bytes that start every PNG file.
    /// </summary>
    private static readonly byte[] _signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>
    /// The table of the CRC-32 of a byte.
    /// </summary>
    private static readonly uint[] _crcTable = BuildCrcTable();

    #endregion // Fields

    #region Methods

    /// <summary>
    /// Encodes an image as PNG.
    /// </summary>
    /// <param name="width">The width in pixels</param>
    /// <param name="height">The height in pixels</param>
    /// <param name="rgba">The pixels, row by row, four bytes per pixel</param>
    /// <returns>The bytes of the PNG file</returns>
    public static byte[] Write(int width, int height, ReadOnlySpan<byte> rgba)
    {
        using var output = new MemoryStream();

        output.Write(_signature);

        Span<byte> header = stackalloc byte[13];

        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header[4..], height);
        header[8] = 8;
        header[9] = 6;
        header[10] = 0;
        header[11] = 0;
        header[12] = 0;

        WriteChunk(output, "IHDR"u8, header);
        WriteChunk(output, "IDAT"u8, Compress(width, height, rgba));
        WriteChunk(output, "IEND"u8, []);

        return output.ToArray();
    }

    /// <summary>
    /// Compresses the pixels with filter 0 for every row.
    /// </summary>
    /// <param name="width">The width in pixels</param>
    /// <param name="height">The height in pixels</param>
    /// <param name="rgba">The pixels</param>
    /// <returns>The zlib stream</returns>
    private static byte[] Compress(int width, int height, ReadOnlySpan<byte> rgba)
    {
        var rowBytes = width * PixelBytes;

        using var compressed = new MemoryStream();

        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, true))
        {
            for (var row = 0; row < height; row++)
            {
                zlib.WriteByte(0);
                zlib.Write(rgba.Slice(row * rowBytes, rowBytes));
            }
        }

        return compressed.ToArray();
    }

    /// <summary>
    /// Writes one chunk.
    /// </summary>
    /// <param name="output">The stream</param>
    /// <param name="type">The four bytes of the chunk type</param>
    /// <param name="data">The data of the chunk</param>
    private static void WriteChunk(Stream output, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> number = stackalloc byte[4];

        BinaryPrimitives.WriteInt32BigEndian(number, data.Length);
        output.Write(number);
        output.Write(type);
        output.Write(data);

        var crc = UpdateCrc(0xFFFFFFFF, type);

        crc = UpdateCrc(crc, data) ^ 0xFFFFFFFF;

        BinaryPrimitives.WriteUInt32BigEndian(number, crc);
        output.Write(number);
    }

    /// <summary>
    /// Continues a CRC-32 over bytes.
    /// </summary>
    /// <param name="crc">The running value</param>
    /// <param name="bytes">The bytes</param>
    /// <returns>The new running value</returns>
    private static uint UpdateCrc(uint crc, ReadOnlySpan<byte> bytes)
    {
        foreach (var value in bytes)
        {
            crc = _crcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }

        return crc;
    }

    /// <summary>
    /// Builds the CRC-32 table.
    /// </summary>
    /// <returns>The table</returns>
    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];

        for (var index = 0u; index < table.Length; index++)
        {
            var value = index;

            for (var bit = 0; bit < 8; bit++)
            {
                value = (value & 1) != 0 ? 0xEDB88320 ^ (value >> 1) : value >> 1;
            }

            table[index] = value;
        }

        return table;
    }

    #endregion // Methods
}