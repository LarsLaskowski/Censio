using System.Security.Cryptography;

namespace Censio.GameData;

/// <summary>
/// Writes and reads the binary catalog cache file.
/// </summary>
public static class GameCatalogCacheFile
{
    #region Constants

    /// <summary>
    /// The version of the file format.
    /// </summary>
    public const int FormatVersion = 1;

    /// <summary>
    /// The largest file size in bytes that is read.
    /// </summary>
    public const long MaxFileBytes = 335544320;

    /// <summary>
    /// The largest size in bytes of everything but the PNG data.
    /// </summary>
    public const long MaxCatalogBytes = 67108864;

    /// <summary>
    /// The largest size in bytes of one string.
    /// </summary>
    public const int MaxStringBytes = 65536;

    /// <summary>
    /// The largest sum in bytes of the decoded sizes of all distinct icons.
    /// </summary>
    private const long MaxDecodedBytes = 4L * GameIconReader.MaxIconSetBytes;

    /// <summary>
    /// The least encoded size of an archive: path length, length and time.
    /// </summary>
    private const int MinArchiveBytes = 20;

    /// <summary>
    /// The least encoded size of an entry: GUID, kind and five flags.
    /// </summary>
    private const int MinEntryBytes = 10;

    /// <summary>
    /// The least encoded size of a distinct icon: sides, length and one byte of PNG data.
    /// </summary>
    private const int MinIconBytes = 13;

    /// <summary>
    /// The size of an integer.
    /// </summary>
    private const int IntBytes = 4;

    /// <summary>
    /// The least encoded size of the assignment of an icon or a problem to a GUID: GUID, tag and value.
    /// </summary>
    private const int MinAssignmentBytes = 9;

    /// <summary>
    /// The tag of an assignment whose value is the index of a distinct icon.
    /// </summary>
    private const byte IconTag = 0;

    /// <summary>
    /// The tag of an assignment whose value is a problem.
    /// </summary>
    private const byte ProblemTag = 1;

    /// <summary>
    /// The least length of a file: magic, version and the hash.
    /// </summary>
    private const int MinFileBytes = 12 + GameCatalogCacheReader.HashLength;

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The eight bytes that start the file.
    /// </summary>
    private static readonly byte[] _magic = "CENSIOCC"u8.ToArray();

    #endregion // Fields

    #region Methods

    /// <summary>
    /// Writes the cache file.
    /// </summary>
    /// <param name="destination">The stream to write to</param>
    /// <param name="key">The key the content belongs to</param>
    /// <param name="catalog">The catalog</param>
    /// <param name="icons">The icons</param>
    /// <returns><c>false</c> when a bound would be exceeded and nothing more was written</returns>
    public static bool Write(Stream destination, GameCatalogKey key, GameCatalog catalog, GameIconSet icons)
    {
        var distinct = icons.Icons.Values.Distinct().ToList();

        if (distinct.Sum(icon => (long)icon.Png.Length) > GameIconReader.MaxIconSetBytes
            || distinct.Sum(icon => GameCatalogCachePng.DecodedBytes(icon.Width, icon.Height)) > MaxDecodedBytes)
        {
            return false;
        }

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        var writer = new GameCatalogCacheWriter(destination, hash);

        writer.WriteRaw(_magic);
        writer.WriteInt32(FormatVersion);
        WriteKey(writer, key);
        WriteCatalog(writer, catalog);
        WriteIcons(writer, icons, distinct);
        writer.WriteHash();

        return writer.IsFailed == false;
    }

    /// <summary>
    /// Reads and validates the cache file as a whole.
    /// </summary>
    /// <param name="source">The stream to read from</param>
    /// <param name="length">The length of the file in bytes</param>
    /// <param name="expected">The key the content must belong to</param>
    /// <returns>The content, or the reason there is none</returns>
    public static GameCatalogCacheResult Read(Stream source, long length, GameCatalogKey expected)
    {
        if (length < MinFileBytes || length > MaxFileBytes)
        {
            return GameCatalogCacheResult.FromKind(GameCatalogCacheResultKind.Invalid);
        }

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        var reader = new GameCatalogCacheReader(source, length, hash);
        var magic = new byte[_magic.Length];

        if (reader.ReadRaw(magic) == false || magic.AsSpan().SequenceEqual(_magic) == false || reader.ReadInt32() != FormatVersion || reader.IsFailed)
        {
            return GameCatalogCacheResult.FromKind(GameCatalogCacheResultKind.Invalid);
        }

        var mismatch = ReadKey(reader, expected);

        if (mismatch is not null)
        {
            return GameCatalogCacheResult.FromKind(mismatch.Value);
        }

        var catalog = ReadCatalog(reader);
        var icons = reader.IsFailed ? null : ReadIcons(reader);

        if (catalog is null || icons is null || reader.Finish() == false)
        {
            return GameCatalogCacheResult.FromKind(GameCatalogCacheResultKind.Invalid);
        }

        return GameCatalogCacheResult.FromContent(catalog, icons);
    }

    /// <summary>
    /// Writes the identity and the game state.
    /// </summary>
    /// <param name="writer">The writer</param>
    /// <param name="key">The key</param>
    private static void WriteKey(GameCatalogCacheWriter writer, GameCatalogKey key)
    {
        writer.WriteString(key.Identity);
        writer.WriteString(key.Folder);
        writer.WriteNullableString(key.Build);
        writer.WriteInt32(key.Archives.Count);

        foreach (var archive in key.Archives)
        {
            writer.WriteString(archive.Path);
            writer.WriteInt64(archive.Length);
            writer.WriteInt64(archive.LastWriteTimeUtc.Ticks);
        }
    }

    /// <summary>
    /// Writes the catalog section.
    /// </summary>
    /// <param name="writer">The writer</param>
    /// <param name="catalog">The catalog</param>
    private static void WriteCatalog(GameCatalogCacheWriter writer, GameCatalog catalog)
    {
        writer.WriteInt32(catalog.Entries.Count);

        foreach (var entry in catalog.Entries)
        {
            writer.WriteInt32(entry.AssetGuid);
            writer.WriteByte((byte)entry.Kind);
            writer.WriteNullableString(entry.EnglishName);
            writer.WriteNullableString(entry.GermanName);
            writer.WriteFlag(entry.RegionGuid.HasValue);

            if (entry.RegionGuid.HasValue)
            {
                writer.WriteInt32(entry.RegionGuid.Value);
            }

            writer.WriteFlag(entry.RegionTypes is not null);

            if (entry.RegionTypes is not null)
            {
                writer.WriteInt32(entry.RegionTypes.Count);

                foreach (var type in entry.RegionTypes)
                {
                    writer.WriteString(type);
                }
            }

            writer.WriteNullableString(entry.IconFilename);
        }

        writer.WriteInt32(catalog.AmbiguousGuids.Count);

        foreach (var guid in catalog.AmbiguousGuids)
        {
            writer.WriteInt32(guid);
        }

        writer.WriteInt32(catalog.InheritedAssetCount);
        writer.WriteInt32(catalog.DroppedRegionTypeCount);
    }

    /// <summary>
    /// Writes the icon section.
    /// </summary>
    /// <param name="writer">The writer</param>
    /// <param name="icons">The icons</param>
    /// <param name="distinct">The distinct icons in the order they are stored</param>
    private static void WriteIcons(GameCatalogCacheWriter writer, GameIconSet icons, List<GameIcon> distinct)
    {
        var indexes = new Dictionary<GameIcon, int>(ReferenceEqualityComparer.Instance);

        writer.WriteInt32(distinct.Count);

        foreach (var icon in distinct)
        {
            indexes[icon] = indexes.Count;
            writer.WriteInt32(icon.Width);
            writer.WriteInt32(icon.Height);
            writer.WriteInt32(icon.Png.Length);
            writer.WritePng(icon.Png);
        }

        writer.WriteInt32(icons.Icons.Count + icons.Problems.Count);

        foreach (var pair in icons.Icons)
        {
            writer.WriteInt32(pair.Key);
            writer.WriteByte(IconTag);
            writer.WriteInt32(indexes[pair.Value]);
        }

        foreach (var pair in icons.Problems)
        {
            writer.WriteInt32(pair.Key);
            writer.WriteByte(ProblemTag);
            writer.WriteInt32((int)pair.Value);
        }

        writer.WriteInt32(icons.GoodsWithIcon);
        writer.WriteInt32(icons.RegionsWithIcon);
    }

    /// <summary>
    /// Reads the identity and the game state and compares them with the expected key, stopping at the first difference.
    /// </summary>
    /// <param name="reader">The reader</param>
    /// <param name="expected">The expected key</param>
    /// <returns><c>null</c> when the key matches, else the kind of the result</returns>
    private static GameCatalogCacheResultKind? ReadKey(GameCatalogCacheReader reader, GameCatalogKey expected)
    {
        var identity = reader.ReadString();

        if (reader.IsFailed)
        {
            return GameCatalogCacheResultKind.Invalid;
        }

        if (string.Equals(identity, expected.Identity, StringComparison.Ordinal) == false)
        {
            return GameCatalogCacheResultKind.CompanionChanged;
        }

        var folder = reader.ReadString();
        var build = reader.ReadNullableString();
        var count = reader.ReadCount(MinArchiveBytes);

        if (reader.IsFailed)
        {
            return GameCatalogCacheResultKind.Invalid;
        }

        if (string.Equals(folder, expected.Folder, StringComparison.OrdinalIgnoreCase) == false
            || string.Equals(build, expected.Build, StringComparison.Ordinal) == false
            || count != expected.Archives.Count)
        {
            return GameCatalogCacheResultKind.GameChanged;
        }

        for (var index = 0; index < count; index++)
        {
            var path = reader.ReadString();
            var archiveLength = reader.ReadInt64();
            var ticks = reader.ReadInt64();

            if (reader.IsFailed)
            {
                return GameCatalogCacheResultKind.Invalid;
            }

            if (GameCatalogKey.IsSameArchive(expected.Archives[index], path, archiveLength, ticks) == false)
            {
                return GameCatalogCacheResultKind.GameChanged;
            }
        }

        return null;
    }

    /// <summary>
    /// Reads the catalog section.
    /// </summary>
    /// <param name="reader">The reader</param>
    /// <returns>The catalog, or <c>null</c> after a failure</returns>
    private static GameCatalog? ReadCatalog(GameCatalogCacheReader reader)
    {
        var count = reader.ReadCount(MinEntryBytes);
        var entries = new List<GameCatalogEntry>();
        var guids = new HashSet<int>();

        for (var index = 0; index < count && reader.IsFailed == false; index++)
        {
            var entry = ReadEntry(reader);

            if (reader.IsFailed == false && guids.Add(entry.AssetGuid) == false)
            {
                reader.Fail();
            }

            entries.Add(entry);
        }

        var ambiguous = new List<int>();
        var ambiguousCount = reader.ReadCount(IntBytes);

        for (var index = 0; index < ambiguousCount && reader.IsFailed == false; index++)
        {
            ambiguous.Add(reader.ReadInt32());
        }

        var inherited = reader.ReadInt32();
        var dropped = reader.ReadInt32();

        if (reader.IsFailed || inherited < 0 || dropped < 0)
        {
            return null;
        }

        return new GameCatalog(entries, ambiguous, inherited, dropped);
    }

    /// <summary>
    /// Reads one entry.
    /// </summary>
    /// <param name="reader">The reader</param>
    /// <returns>The entry, with arbitrary content after a failure</returns>
    private static GameCatalogEntry ReadEntry(GameCatalogCacheReader reader)
    {
        var guid = reader.ReadInt32();
        var kind = reader.ReadByte();

        if (Enum.IsDefined((GameCatalogKind)kind) == false)
        {
            reader.Fail();
        }

        var english = reader.ReadNullableString();
        var german = reader.ReadNullableString();
        var regionGuid = reader.ReadFlag() ? reader.ReadInt32() : (int?)null;
        var regionTypes = reader.ReadFlag() ? ReadStrings(reader) : null;
        var iconFilename = reader.ReadNullableString();

        return new GameCatalogEntry
               {
                   AssetGuid = guid,
                   Kind = (GameCatalogKind)kind,
                   EnglishName = english,
                   GermanName = german,
                   RegionGuid = regionGuid,
                   RegionTypes = regionTypes,
                   IconFilename = iconFilename
               };
    }

    /// <summary>
    /// Reads a counted list of strings.
    /// </summary>
    /// <param name="reader">The reader</param>
    /// <returns>The strings</returns>
    private static List<string> ReadStrings(GameCatalogCacheReader reader)
    {
        var count = reader.ReadCount(IntBytes);
        var strings = new List<string>();

        for (var index = 0; index < count && reader.IsFailed == false; index++)
        {
            strings.Add(reader.ReadString());
        }

        return strings;
    }

    /// <summary>
    /// Reads the icon section, checking every icon as it is read.
    /// </summary>
    /// <param name="reader">The reader</param>
    /// <returns>The icons, or <c>null</c> after a failure</returns>
    private static GameIconSet? ReadIcons(GameCatalogCacheReader reader)
    {
        var distinct = ReadDistinctIcons(reader);
        var count = reader.ReadCount(MinAssignmentBytes);
        var icons = new Dictionary<int, GameIcon>();
        var problems = new Dictionary<int, GameIconProblem>();
        var used = new bool[distinct.Count];

        for (var index = 0; index < count && reader.IsFailed == false; index++)
        {
            var guid = reader.ReadInt32();
            var tag = reader.ReadByte();
            var value = reader.ReadInt32();

            if (reader.IsFailed || Assign(guid, tag, value, distinct, icons, problems, used) == false)
            {
                reader.Fail();
            }
        }

        var goods = reader.ReadInt32();
        var regions = reader.ReadInt32();

        if (reader.IsFailed || goods < 0 || regions < 0 || used.Contains(false))
        {
            return null;
        }

        return new GameIconSet(icons, problems, goods, regions);
    }

    /// <summary>
    /// Assigns an icon or a problem to an asset.
    /// </summary>
    /// <param name="guid">The asset GUID</param>
    /// <param name="tag">Whether the value is an icon index or a problem</param>
    /// <param name="value">The icon index or the problem</param>
    /// <param name="distinct">The distinct icons</param>
    /// <param name="icons">The icons by GUID</param>
    /// <param name="problems">The problems by GUID</param>
    /// <param name="used">Which distinct icons are used</param>
    /// <returns><c>false</c> when the assignment is invalid or repeats a GUID</returns>
    private static bool Assign(int guid, byte tag, int value, List<GameIcon> distinct, Dictionary<int, GameIcon> icons, Dictionary<int, GameIconProblem> problems, bool[] used)
    {
        if (tag == IconTag)
        {
            if (value < 0 || value >= distinct.Count || icons.TryAdd(guid, distinct[value]) == false)
            {
                return false;
            }

            used[value] = true;

            return true;
        }

        return tag == ProblemTag && Enum.IsDefined((GameIconProblem)value) && problems.TryAdd(guid, (GameIconProblem)value);
    }

    /// <summary>
    /// Reads the distinct icons.
    /// </summary>
    /// <param name="reader">The reader</param>
    /// <returns>The icons read before a failure or all of them</returns>
    private static List<GameIcon> ReadDistinctIcons(GameCatalogCacheReader reader)
    {
        var count = reader.ReadCount(MinIconBytes);
        var icons = new List<GameIcon>();
        var decodedBytes = 0L;

        for (var index = 0; index < count && reader.IsFailed == false; index++)
        {
            var width = reader.ReadInt32();
            var height = reader.ReadInt32();
            var pngLength = reader.ReadInt32();

            decodedBytes += GameCatalogCachePng.DecodedBytes(width, height);

            if (reader.IsFailed || decodedBytes > MaxDecodedBytes)
            {
                reader.Fail();

                break;
            }

            var png = reader.ReadPng(pngLength);

            if (png is null || GameCatalogCachePng.IsValid(width, height, png) == false)
            {
                reader.Fail();

                break;
            }

            icons.Add(new GameIcon(width, height, png));
        }

        return icons;
    }

    #endregion // Methods
}