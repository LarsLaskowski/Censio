using System.IO.Compression;

namespace Censio.GameData.Tests;

/// <summary>
/// Builds PNG files that differ from the Companion's own PNG writer in exactly one way, each named by a row.
/// </summary>
internal static class BrokenPngKit
{
    #region Constants

    /// <summary>
    /// The width of the base image.
    /// </summary>
    private const int Width = 6;

    /// <summary>
    /// The height of the base image.
    /// </summary>
    private const int Height = 5;

    #endregion // Constants

    #region Methods

    /// <summary>
    /// Builds the icon of a row, with the sizes stored beside the PNG.
    /// </summary>
    /// <param name="row">The name of the row</param>
    /// <returns>The icon</returns>
    public static GameIcon Build(string row)
    {
        var rgba = CacheTestKit.Noise(Width, Height, 7);
        var raw = CacheTestKit.Scanlines(Width, Height, rgba);
        var zlib = CacheTestKit.Zlib(raw);
        var ihdr = CacheTestKit.Chunk("IHDR", CacheTestKit.IhdrData(Width, Height));
        var idat = CacheTestKit.Chunk("IDAT", zlib);
        var iend = CacheTestKit.Chunk("IEND", []);
        var signature = CacheTestKit.Signature();
        var half = zlib.Length / 2;

        return row switch
               {
                   "signature" => Icon(Flip(CacheTestKit.HandBuiltPng(Width, Height, rgba), 0)),
                   "ihdrLength12" => Icon(CacheTestKit.Concat(signature, CacheTestKit.Chunk("IHDR", CacheTestKit.IhdrData(Width, Height)[..12]), idat, iend)),
                   "ihdrLength14" => Icon(CacheTestKit.Concat(signature, CacheTestKit.Chunk("IHDR", [.. CacheTestKit.IhdrData(Width, Height), 0]), idat, iend)),
                   "sideZero" => Sided(0, 1),
                   "side2049" => Sided(2049, 1),
                   "bitDepth16" => Fields(rgba, [16, 6, 0, 0, 0]),
                   "colorType2" => Fields(rgba, [8, 2, 0, 0, 0]),
                   "compression1" => Fields(rgba, [8, 6, 1, 0, 0]),
                   "filter1" => Fields(rgba, [8, 6, 0, 1, 0]),
                   "interlace1" => Fields(rgba, [8, 6, 0, 0, 1]),
                   "sidesDifferFromStored" => new GameIcon(Height, Width, CacheTestKit.HandBuiltPng(Width, Height, rgba)),
                   "extraChunkBeforeIdat" => Icon(CacheTestKit.Concat(signature, ihdr, CacheTestKit.Chunk("tEXt", [97, 0, 98]), idat, iend)),
                   "extraChunkAfterIdat" => Icon(CacheTestKit.Concat(signature, ihdr, idat, CacheTestKit.Chunk("tEXt", [97, 0, 98]), iend)),
                   "twoIdat" => Icon(CacheTestKit.Concat(signature, ihdr, CacheTestKit.Chunk("IDAT", zlib[..half]), CacheTestKit.Chunk("IDAT", zlib[half..]), iend)),
                   "idatBeforeIhdr" => Icon(CacheTestKit.Concat(signature, idat, ihdr, iend)),
                   "iendBeforeIdat" => Icon(CacheTestKit.Concat(signature, ihdr, iend, idat)),
                   "crcIhdr" => Icon(CacheTestKit.Concat(signature, Flip(ihdr, ihdr.Length - 1), idat, iend)),
                   "crcIdat" => Icon(CacheTestKit.Concat(signature, ihdr, Flip(idat, idat.Length - 1), iend)),
                   "crcIend" => Icon(CacheTestKit.Concat(signature, ihdr, idat, Flip(iend, iend.Length - 1))),
                   "iendWithData" => Icon(CacheTestKit.Concat(signature, ihdr, idat, CacheTestKit.Chunk("IEND", [0]))),
                   "byteAfterIend" => Icon(CacheTestKit.Concat(CacheTestKit.HandBuiltPng(Width, Height, rgba), [0])),
                   "presetDictionary" => Idat(CacheTestKit.Concat([0x78, 0xBB, 0, 0, 0, 0], zlib[2..^4], zlib[^4..])),
                   "brokenDeflate" => Idat(Flip(zlib, zlib.Length / 2)),
                   "rowFilter1" => Idat(CacheTestKit.Zlib(CacheTestKit.Scanlines(Width, Height, rgba, 1))),
                   "rowFilter2" => Idat(CacheTestKit.Zlib(CacheTestKit.Scanlines(Width, Height, rgba, 2))),
                   "rowFilter3" => Idat(CacheTestKit.Zlib(CacheTestKit.Scanlines(Width, Height, rgba, 3))),
                   "rowFilter4" => Idat(CacheTestKit.Zlib(CacheTestKit.Scanlines(Width, Height, rgba, 4))),
                   "decodedShort" => Idat(CacheTestKit.Zlib(raw[..^1])),
                   "decodedLong" => Idat(CacheTestKit.Zlib([.. raw, 0])),
                   "bytesAfterZlibEnd" => Idat([.. zlib, 0]),
                   "otherCompression" => Idat(CacheTestKit.Zlib(raw, CompressionLevel.NoCompression)),
                   _ => throw new ArgumentException("Unknown row " + row, nameof(row))
               };
    }

    /// <summary>
    /// Builds the PNG of the base image with a header chunk of other fields.
    /// </summary>
    /// <param name="rgba">The pixels</param>
    /// <param name="fields">The five header fields after the sides</param>
    /// <returns>The icon</returns>
    private static GameIcon Fields(byte[] rgba, byte[] fields)
    {
        var raw = CacheTestKit.Scanlines(Width, Height, rgba);

        return Icon(CacheTestKit.Concat(CacheTestKit.Signature(), CacheTestKit.Chunk("IHDR", CacheTestKit.IhdrData(Width, Height, fields)), CacheTestKit.Chunk("IDAT", CacheTestKit.Zlib(raw)), CacheTestKit.Chunk("IEND", [])));
    }

    /// <summary>
    /// Builds the PNG of an image of a size outside the bounds.
    /// </summary>
    /// <param name="width">The width</param>
    /// <param name="height">The height</param>
    /// <returns>The icon with the same sizes stored</returns>
    private static GameIcon Sided(int width, int height)
    {
        var rgba = new byte[width * height * 4];

        return new GameIcon(width, height, CacheTestKit.HandBuiltPng(width, height, rgba));
    }

    /// <summary>
    /// Builds the PNG of the base image around a given zlib stream.
    /// </summary>
    /// <param name="zlib">The content of the data chunk</param>
    /// <returns>The icon</returns>
    private static GameIcon Idat(byte[] zlib)
    {
        return Icon(CacheTestKit.Concat(CacheTestKit.Signature(), CacheTestKit.Chunk("IHDR", CacheTestKit.IhdrData(Width, Height)), CacheTestKit.Chunk("IDAT", zlib), CacheTestKit.Chunk("IEND", [])));
    }

    /// <summary>
    /// Creates the icon of the base size around PNG bytes.
    /// </summary>
    /// <param name="png">The bytes</param>
    /// <returns>The icon</returns>
    private static GameIcon Icon(byte[] png)
    {
        return new GameIcon(Width, Height, png);
    }

    /// <summary>
    /// Copies bytes and changes the lowest bit of one.
    /// </summary>
    /// <param name="bytes">The bytes</param>
    /// <param name="index">The index</param>
    /// <returns>The copy</returns>
    private static byte[] Flip(byte[] bytes, int index)
    {
        var copy = (byte[])bytes.Clone();

        copy[index] ^= 1;

        return copy;
    }

    #endregion // Methods
}