using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Censio.GameData.Tests;

/// <summary>
/// Builds keys, catalogs, icons, cache files and hand-made PNG files for the tests of the catalog cache, and changes
/// the bytes of a written cache file.
/// </summary>
internal static class CacheTestKit
{
    #region Constants

    /// <summary>
    /// The GUID of the first entry of the sample catalog.
    /// </summary>
    public const int GuidAlpha = 0x1A2B3C4D;

    /// <summary>
    /// The GUID of the second entry of the sample catalog.
    /// </summary>
    public const int GuidBeta = 0x2B3C4D5E;

    /// <summary>
    /// The GUID of the third entry of the sample catalog.
    /// </summary>
    public const int GuidGamma = 0x3C4D5E6F;

    /// <summary>
    /// The GUID of the asset that is ambiguous and has an icon problem.
    /// </summary>
    public const int GuidAmbiguous = 0x4D5E6F70;

    /// <summary>
    /// The length of the hash at the end of a file.
    /// </summary>
    public const int HashLength = 32;

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
    /// Creates the key of the tests; it is small, so that most of a file is catalog and icons.
    /// </summary>
    /// <param name="identity">The Companion identity</param>
    /// <param name="build">The build</param>
    /// <returns>The key</returns>
    public static GameCatalogKey Key(string identity = "idA", string? build = "b1")
    {
        return new GameCatalogKey(identity, "f", build, [Archive("a.rda", 7, 0)]);
    }

    /// <summary>
    /// Creates one archive state.
    /// </summary>
    /// <param name="path">The path</param>
    /// <param name="length">The length</param>
    /// <param name="seconds">The seconds after a fixed time</param>
    /// <returns>The state</returns>
    public static GameArchiveState Archive(string path, long length, int seconds)
    {
        return new GameArchiveState
               {
                   Path = path,
                   Length = length,
                   LastWriteTimeUtc = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc).AddSeconds(seconds)
               };
    }

    /// <summary>
    /// Creates the sample catalog: one entry with every nullable field null, one with every field filled and one with
    /// empty strings and an empty list.
    /// </summary>
    /// <returns>The catalog</returns>
    public static GameCatalog SampleCatalog()
    {
        var entries = new List<GameCatalogEntry>
                      {
                          new()
                          {
                              AssetGuid = GuidAlpha,
                              Kind = GameCatalogKind.Good,
                              EnglishName = "Alpha-Name",
                              GermanName = "Alpha-De",
                              IconFilename = "icon_alpha"
                          },
                          new()
                          {
                              AssetGuid = GuidBeta,
                              Kind = GameCatalogKind.Region,
                              RegionGuid = 0x33445566,
                              RegionTypes = ["Moderate", "Colony01"]
                          },
                          new()
                          {
                              AssetGuid = GuidGamma,
                              Kind = GameCatalogKind.Building,
                              EnglishName = string.Empty,
                              GermanName = string.Empty,
                              RegionTypes = [],
                              IconFilename = string.Empty
                          }
                      };

        return new GameCatalog(entries, [GuidAmbiguous], 5, 2);
    }

    /// <summary>
    /// Creates the sample icons: one icon shared by two assets, a second icon and one problem.
    /// </summary>
    /// <returns>The icons</returns>
    public static GameIconSet SampleIcons()
    {
        var shared = NoiseIcon(24, 24, 1);
        var other = NoiseIcon(8, 6, 2);

        var icons = new Dictionary<int, GameIcon>
                    {
                        [GuidAlpha] = shared,
                        [GuidBeta] = shared,
                        [GuidGamma] = other
                    };

        var problems = new Dictionary<int, GameIconProblem>
                       {
                           [GuidAmbiguous] = GameIconProblem.Missing
                       };

        return new GameIconSet(icons, problems, 2, 1);
    }

    /// <summary>
    /// Creates an icon of seeded noise.
    /// </summary>
    /// <param name="width">The width</param>
    /// <param name="height">The height</param>
    /// <param name="seed">The seed</param>
    /// <returns>The icon</returns>
    public static GameIcon NoiseIcon(int width, int height, int seed)
    {
        return new GameIcon(width, height, PngWriter.Write(width, height, Noise(width, height, seed)));
    }

    /// <summary>
    /// Creates seeded noise pixels.
    /// </summary>
    /// <param name="width">The width</param>
    /// <param name="height">The height</param>
    /// <param name="seed">The seed</param>
    /// <returns>The pixels</returns>
    public static byte[] Noise(int width, int height, int seed)
    {
        var rgba = new byte[width * height * PixelBytes];

        new Random(seed).NextBytes(rgba);

        return rgba;
    }

    /// <summary>
    /// Writes a cache file and requires that the writer reports success.
    /// </summary>
    /// <param name="key">The key</param>
    /// <param name="catalog">The catalog</param>
    /// <param name="icons">The icons</param>
    /// <returns>The bytes of the file</returns>
    public static byte[] WriteFile(GameCatalogKey key, GameCatalog catalog, GameIconSet icons)
    {
        using var stream = new MemoryStream();

        var isWritten = GameCatalogCacheFile.Write(stream, key, catalog, icons);

        Assert.IsTrue(isWritten, "The writer should accept the content of the test");

        return stream.ToArray();
    }

    /// <summary>
    /// Writes the cache file of the sample catalog and icons.
    /// </summary>
    /// <returns>The bytes of the file</returns>
    public static byte[] WriteSample()
    {
        return WriteFile(Key(), SampleCatalog(), SampleIcons());
    }

    /// <summary>
    /// Reads bytes as a cache file whose length is the length of the bytes.
    /// </summary>
    /// <param name="bytes">The bytes</param>
    /// <param name="expected">The expected key</param>
    /// <returns>The result</returns>
    public static GameCatalogCacheResult Read(byte[] bytes, GameCatalogKey expected)
    {
        using var stream = new MemoryStream(bytes, false);

        return GameCatalogCacheFile.Read(stream, bytes.Length, expected);
    }

    /// <summary>
    /// Replaces the hash at the end of a file by the SHA-256 of everything before it, so that only the change a test
    /// made can make the file invalid.
    /// </summary>
    /// <param name="bytes">The bytes, changed in place</param>
    /// <returns>The same bytes</returns>
    public static byte[] Reseal(byte[] bytes)
    {
        var hash = SHA256.HashData(bytes.AsSpan(0, bytes.Length - HashLength));

        hash.CopyTo(bytes, bytes.Length - HashLength);

        return bytes;
    }

    /// <summary>
    /// Finds the only place of a byte sequence and requires that there is exactly one.
    /// </summary>
    /// <param name="bytes">The bytes</param>
    /// <param name="pattern">The sequence</param>
    /// <returns>The offset</returns>
    public static int FindOnly(byte[] bytes, byte[] pattern)
    {
        var first = bytes.AsSpan().IndexOf(pattern);

        Assert.IsGreaterThanOrEqualTo(0, first, "The file should hold the sequence the test looks for");
        Assert.AreEqual(-1, bytes.AsSpan(first + 1).IndexOf(pattern), "The file should hold the sequence the test looks for once");

        return first;
    }

    /// <summary>
    /// Finds the first place of a byte sequence and requires that there is one.
    /// </summary>
    /// <param name="bytes">The bytes</param>
    /// <param name="pattern">The sequence</param>
    /// <returns>The offset</returns>
    public static int FindFirst(byte[] bytes, byte[] pattern)
    {
        var first = bytes.AsSpan().IndexOf(pattern);

        Assert.IsGreaterThanOrEqualTo(0, first, "The file should hold the sequence the test looks for");

        return first;
    }

    /// <summary>
    /// Encodes an integer as four little-endian bytes.
    /// </summary>
    /// <param name="value">The value</param>
    /// <returns>The bytes</returns>
    public static byte[] Int32Bytes(int value)
    {
        var bytes = new byte[4];

        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);

        return bytes;
    }

    /// <summary>
    /// Compares two catalogs entry by entry, including which fields are null.
    /// </summary>
    /// <param name="expected">The expected catalog</param>
    /// <param name="actual">The actual catalog</param>
    public static void AssertSameCatalog(GameCatalog expected, GameCatalog? actual)
    {
        Assert.IsNotNull(actual, "The catalog should be loaded");
        Assert.HasCount(expected.Count, actual.Entries, "The entry count should survive");
        Assert.AreEqual(expected.InheritedAssetCount, actual.InheritedAssetCount, "The inherited count should survive");
        Assert.AreEqual(expected.DroppedRegionTypeCount, actual.DroppedRegionTypeCount, "The dropped region type count should survive");
        Assert.AreSequenceEqual(expected.AmbiguousGuids.Order().ToList(), actual.AmbiguousGuids.Order().ToList(), "The ambiguous GUIDs should survive");

        foreach (var wanted in expected.Entries)
        {
            var entry = actual.Entries.SingleOrDefault(candidate => candidate.AssetGuid == wanted.AssetGuid);

            Assert.IsNotNull(entry, $"The entry {wanted.AssetGuid} should survive");
            Assert.AreEqual(wanted.Kind, entry.Kind, "The kind should survive");
            Assert.AreEqual(wanted.EnglishName, entry.EnglishName, "The English name should survive, null staying null and empty staying empty");
            Assert.AreEqual(wanted.GermanName, entry.GermanName, "The German name should survive, null staying null and empty staying empty");
            Assert.AreEqual(wanted.RegionGuid, entry.RegionGuid, "The region GUID should survive, null staying null");
            Assert.AreEqual(wanted.IconFilename, entry.IconFilename, "The icon file name should survive, null staying null and empty staying empty");
            Assert.AreEqual(wanted.RegionTypes is null, entry.RegionTypes is null, "The region types should stay null or present");
            Assert.AreSequenceEqual(wanted.RegionTypes ?? [], entry.RegionTypes ?? [], "The region types should survive");
        }
    }

    /// <summary>
    /// Compares two icon sets: the PNG bytes, the sizes, the sharing, the problems and the counts.
    /// </summary>
    /// <param name="expected">The expected icons</param>
    /// <param name="actual">The actual icons</param>
    public static void AssertSameIcons(GameIconSet expected, GameIconSet? actual)
    {
        Assert.IsNotNull(actual, "The icons should be loaded");
        Assert.HasCount(expected.Icons.Count, actual.Icons, "The icon count should survive");
        Assert.AreEqual(expected.GoodsWithIcon, actual.GoodsWithIcon, "The goods count should survive");
        Assert.AreEqual(expected.RegionsWithIcon, actual.RegionsWithIcon, "The regions count should survive");
        Assert.AreSequenceEqual(expected.Problems.OrderBy(pair => pair.Key).ToList(), actual.Problems.OrderBy(pair => pair.Key).ToList(), "The problems should survive");

        foreach (var pair in expected.Icons)
        {
            var icon = actual.Find(pair.Key);

            Assert.IsNotNull(icon, $"The icon of {pair.Key} should survive");
            Assert.AreEqual(pair.Value.Width, icon.Width, "The width should survive");
            Assert.AreEqual(pair.Value.Height, icon.Height, "The height should survive");
            Assert.AreSequenceEqual(pair.Value.Png.ToArray(), icon.Png.ToArray(), "The PNG bytes should survive");
        }
    }

    /// <summary>
    /// Builds a PNG chunk with a correct CRC.
    /// </summary>
    /// <param name="type">The four-letter type</param>
    /// <param name="data">The data</param>
    /// <returns>The bytes of the chunk</returns>
    public static byte[] Chunk(string type, byte[] data)
    {
        var typeBytes = Encoding.ASCII.GetBytes(type);
        var result = new byte[12 + data.Length];

        BinaryPrimitives.WriteInt32BigEndian(result, data.Length);
        typeBytes.CopyTo(result, 4);
        data.CopyTo(result, 8);

        var crc = UpdateCrc(0xFFFFFFFF, result.AsSpan(4, 4 + data.Length)) ^ 0xFFFFFFFF;

        BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(8 + data.Length), crc);

        return result;
    }

    /// <summary>
    /// Builds the data of a header chunk.
    /// </summary>
    /// <param name="width">The width</param>
    /// <param name="height">The height</param>
    /// <param name="fields">The five bytes after the sides: depth, colour type, compression, filter, interlace</param>
    /// <returns>The data</returns>
    public static byte[] IhdrData(int width, int height, byte[]? fields = null)
    {
        var data = new byte[13];

        BinaryPrimitives.WriteInt32BigEndian(data, width);
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(4), height);
        (fields ?? [8, 6, 0, 0, 0]).CopyTo(data, 8);

        return data;
    }

    /// <summary>
    /// Builds the scanlines of an image with the same filter byte in front of every row.
    /// </summary>
    /// <param name="width">The width</param>
    /// <param name="height">The height</param>
    /// <param name="rgba">The pixels</param>
    /// <param name="filter">The filter byte</param>
    /// <returns>The scanlines</returns>
    public static byte[] Scanlines(int width, int height, byte[] rgba, byte filter = 0)
    {
        var rowBytes = width * PixelBytes;
        var result = new byte[height * (1 + rowBytes)];

        for (var row = 0; row < height; row++)
        {
            result[row * (1 + rowBytes)] = filter;
            rgba.AsSpan(row * rowBytes, rowBytes).CopyTo(result.AsSpan((row * (1 + rowBytes)) + 1));
        }

        return result;
    }

    /// <summary>
    /// Compresses bytes as a zlib stream.
    /// </summary>
    /// <param name="raw">The bytes</param>
    /// <param name="level">The compression level</param>
    /// <returns>The zlib stream</returns>
    public static byte[] Zlib(byte[] raw, CompressionLevel level = CompressionLevel.Optimal)
    {
        using var compressed = new MemoryStream();

        using (var zlib = new ZLibStream(compressed, level, true))
        {
            zlib.Write(raw);
        }

        return compressed.ToArray();
    }

    /// <summary>
    /// Joins byte arrays.
    /// </summary>
    /// <param name="parts">The parts</param>
    /// <returns>The joined bytes</returns>
    public static byte[] Concat(params byte[][] parts)
    {
        return [.. parts.SelectMany(part => part)];
    }

    /// <summary>
    /// Gets the PNG signature.
    /// </summary>
    /// <returns>The signature</returns>
    public static byte[] Signature()
    {
        return [.. _signature];
    }

    /// <summary>
    /// Builds a PNG the way the Companion's writer does, from its own parts.
    /// </summary>
    /// <param name="width">The width</param>
    /// <param name="height">The height</param>
    /// <param name="rgba">The pixels</param>
    /// <returns>The PNG</returns>
    public static byte[] HandBuiltPng(int width, int height, byte[] rgba)
    {
        return Concat(Signature(), Chunk("IHDR", IhdrData(width, height)), Chunk("IDAT", Zlib(Scanlines(width, height, rgba))), Chunk("IEND", []));
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