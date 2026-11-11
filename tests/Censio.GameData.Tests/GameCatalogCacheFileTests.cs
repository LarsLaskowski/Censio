using System.Text;

namespace Censio.GameData.Tests;

/// <summary>
/// Tests for <see cref="GameCatalogCacheFile" />. A damaged file is made from a written one by changing bytes and, where only
/// that change may refuse it, writing the hash again; the tests assume the layout of record 0053: little-endian numbers, a
/// four-byte length in front of a string, the entry GUID followed by the kind, the magic and the format version first.
/// </summary>
[TestClass]
public sealed class GameCatalogCacheFileTests
{
    #region Constants

    /// <summary>
    /// The number of bytes the reader may read beyond the end of the first invalid icon.
    /// </summary>
    private const int ReadAhead = 65536;

    /// <summary>
    /// The size of the large icons that follow the invalid one.
    /// </summary>
    private const int LargeSide = 512;

    /// <summary>
    /// The number of entries that make the catalog of the write test as large as its bound allows.
    /// </summary>
    private const int EntriesBelowBound = 500;

    /// <summary>
    /// The size in bytes of the PNG data of one icon of the PNG bound test.
    /// </summary>
    private const int Piece = 31457280;

    #endregion // Constants

    #region Methods

    #region AC1

    /// <summary>
    /// A written file is read back as the same catalog and the same icons.
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void GameCatalogCacheFileReadRoundTripsCatalogAndIcons()
    {
        // Arrange
        var bytes = CacheTestKit.WriteSample();

        // Act
        var result = CacheTestKit.Read(bytes, CacheTestKit.Key());

        // Assert
        Assert.AreEqual(GameCatalogCacheResultKind.Loaded, result.Kind, "A file of the same key should be loaded");
        CacheTestKit.AssertSameCatalog(CacheTestKit.SampleCatalog(), result.Catalog);
        CacheTestKit.AssertSameIcons(CacheTestKit.SampleIcons(), result.Icons);
    }

    /// <summary>
    /// Every nullable field of an entry stays null when it was null and stays empty when it was empty, and a region GUID of zero is
    /// not null.
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void GameCatalogCacheFileReadRoundTripsNullAndEmptyFields()
    {
        // Arrange
        var entries = new List<GameCatalogEntry>
                      {
                          new()
                          {
                              AssetGuid = 1,
                              Kind = GameCatalogKind.Workforce
                          },
                          new()
                          {
                              AssetGuid = 2,
                              Kind = GameCatalogKind.Good,
                              EnglishName = string.Empty,
                              GermanName = string.Empty,
                              RegionGuid = 0,
                              RegionTypes = [],
                              IconFilename = string.Empty
                          },
                          new()
                          {
                              AssetGuid = 3,
                              Kind = GameCatalogKind.Region,
                              EnglishName = "e",
                              RegionTypes = [string.Empty, "Moderate"]
                          },
                          new()
                          {
                              AssetGuid = 4,
                              Kind = GameCatalogKind.Building,
                              GermanName = "g",
                              RegionGuid = int.MinValue
                          }
                      };
        var catalog = new GameCatalog(entries, [], 0, 0);
        var icons = new GameIconSet(new Dictionary<int, GameIcon>(), new Dictionary<int, GameIconProblem>(), 0, 0);
        var bytes = CacheTestKit.WriteFile(CacheTestKit.Key(), catalog, icons);

        // Act
        var result = CacheTestKit.Read(bytes, CacheTestKit.Key());

        // Assert
        Assert.AreEqual(GameCatalogCacheResultKind.Loaded, result.Kind, "The file should be loaded");
        CacheTestKit.AssertSameCatalog(catalog, result.Catalog);
        CacheTestKit.AssertSameIcons(icons, result.Icons);
    }

    /// <summary>
    /// A catalog and an icon set without content round-trip.
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void GameCatalogCacheFileReadEmptyContentLoads()
    {
        // Arrange
        var catalog = new GameCatalog([], [], 0, 0);
        var icons = new GameIconSet(new Dictionary<int, GameIcon>(), new Dictionary<int, GameIconProblem>(), 0, 0);
        var bytes = CacheTestKit.WriteFile(CacheTestKit.Key("i", null), catalog, icons);

        // Act
        var result = CacheTestKit.Read(bytes, CacheTestKit.Key("i", null));

        // Assert
        Assert.AreEqual(GameCatalogCacheResultKind.Loaded, result.Kind, "An empty content with an unknown build should be loaded");
        Assert.IsEmpty(result.Catalog!.Entries, "There should be no entries");
        Assert.IsEmpty(result.Icons!.Icons, "There should be no icons");
    }

    /// <summary>
    /// Two assets that shared one icon share one icon again, so the PNG data is held once.
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void GameCatalogCacheFileReadSharedIconStaysShared()
    {
        // Arrange
        var bytes = CacheTestKit.WriteSample();

        // Act
        var result = CacheTestKit.Read(bytes, CacheTestKit.Key());

        // Assert
        var first = result.Icons!.Find(CacheTestKit.GuidAlpha);
        var second = result.Icons.Find(CacheTestKit.GuidBeta);
        var other = result.Icons.Find(CacheTestKit.GuidGamma);

        Assert.AreSame(first, second, "Assets that shared an icon should share it again");
        Assert.AreNotSame(first, other, "Assets with different icons should not share one");
        Assert.AreEqual(first!.Png.Length + other!.Png.Length, result.Icons.PngBytes, "The distinct PNG data should be counted once");
    }

    /// <summary>
    /// A stream that cannot seek and does not know its length is read to the end.
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void GameCatalogCacheFileReadNonSeekableStreamLoads()
    {
        // Arrange
        var bytes = CacheTestKit.WriteSample();
        using var stream = new NonSeekableStream(new MemoryStream(bytes));

        // Act
        var result = GameCatalogCacheFile.Read(stream, bytes.Length, CacheTestKit.Key());

        // Assert
        Assert.AreEqual(GameCatalogCacheResultKind.Loaded, result.Kind, "A forward-only stream should be read");
        CacheTestKit.AssertSameCatalog(CacheTestKit.SampleCatalog(), result.Catalog);
    }

    /// <summary>
    /// The reader stops at the first invalid icon: with megabytes of PNG data after it, the stream is not read much beyond it.
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    [TestCategory("AC6")]
    public void GameCatalogCacheFileReadStopsAtFirstInvalidIcon()
    {
        // Arrange
        var entries = Enumerable.Range(1, 7).Select(guid => new GameCatalogEntry
                                                            {
                                                                AssetGuid = guid,
                                                                Kind = GameCatalogKind.Good
                                                            }).ToList();
        var icons = new Dictionary<int, GameIcon>
                    {
                        [1] = CacheTestKit.NoiseIcon(16, 16, 11)
                    };

        foreach (var guid in Enumerable.Range(2, 6))
        {
            icons[guid] = CacheTestKit.NoiseIcon(LargeSide, LargeSide, guid);
        }

        var bytes = CacheTestKit.WriteFile(CacheTestKit.Key(), new GameCatalog(entries, [], 0, 0), new GameIconSet(icons, new Dictionary<int, GameIconProblem>(), 7, 0));
        var start = CacheTestKit.FindFirst(bytes, CacheTestKit.Signature());
        var end = start + icons[1].Png.Length;

        bytes[end - 1] ^= 1;
        CacheTestKit.Reseal(bytes);

        using var stream = new MemoryStream(bytes, false);

        // Act
        var result = GameCatalogCacheFile.Read(stream, bytes.Length, CacheTestKit.Key());

        // Assert
        Assert.AreEqual(GameCatalogCacheResultKind.Invalid, result.Kind, "A file with an invalid icon should be invalid");
        Assert.IsGreaterThan(3 * 1024 * 1024, bytes.Length - end, "The test file should hold megabytes of PNG data after the invalid icon");
        Assert.IsLessThanOrEqualTo(end + ReadAhead, stream.Position, "The reader should stop at the invalid icon and not read the rest of the file");
    }

    #endregion // AC1

    #region AC2

    /// <summary>
    /// A file written by another Companion identity is reported as such.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void GameCatalogCacheFileReadOtherIdentityReturnsCompanionChanged()
    {
        // Arrange
        var bytes = CacheTestKit.WriteSample();

        // Act
        var result = CacheTestKit.Read(bytes, CacheTestKit.Key("idB"));

        // Assert
        AssertNothingLoaded(result, GameCatalogCacheResultKind.CompanionChanged, "Another identity should be reported as a changed Companion");
    }

    /// <summary>
    /// A file written for another game state is reported as such.
    /// </summary>
    /// <param name="difference">The difference</param>
    [TestMethod]
    [TestCategory("AC2")]
    [DataRow("build")]
    [DataRow("buildUnknown")]
    [DataRow("folder")]
    [DataRow("length")]
    [DataRow("time")]
    [DataRow("added")]
    [DataRow("removed")]
    public void GameCatalogCacheFileReadOtherGameReturnsGameChanged(string difference)
    {
        // Arrange
        var bytes = CacheTestKit.WriteSample();

        // Act
        var result = CacheTestKit.Read(bytes, ChangedGame(difference));

        // Assert
        AssertNothingLoaded(result, GameCatalogCacheResultKind.GameChanged, $"The difference '{difference}' should be reported as a changed game");
    }

    /// <summary>
    /// When both the identity and the game differ, the Companion change is reported.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void GameCatalogCacheFileReadIdentityAndGameDifferReturnsCompanionChanged()
    {
        // Arrange
        var bytes = CacheTestKit.WriteSample();
        var expected = new GameCatalogKey("idB", "other", "b2", []);

        // Act
        var result = CacheTestKit.Read(bytes, expected);

        // Assert
        AssertNothingLoaded(result, GameCatalogCacheResultKind.CompanionChanged, "The Companion change should win over the game change");
    }

    #endregion // AC2

    #region AC5

    /// <summary>
    /// A string above its bound is not written, whichever field holds it and whether it is too long in bytes but not in characters.
    /// </summary>
    /// <param name="field">The field</param>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow("englishName")]
    [DataRow("germanName")]
    [DataRow("iconFilename")]
    [DataRow("regionType")]
    [DataRow("englishNameMultiByte")]
    [DataRow("keyIdentity")]
    [DataRow("keyFolder")]
    [DataRow("keyBuild")]
    [DataRow("archivePath")]
    public void GameCatalogCacheFileWriteAboveBoundReturnsFalse(string field)
    {
        // Arrange
        var (key, catalog) = WithLongString(field, GameCatalogCacheFile.MaxStringBytes + 1);

        // Act
        var isWritten = GameCatalogCacheFile.Write(Stream.Null, key, catalog, EmptyIcons());

        // Assert
        Assert.IsFalse(isWritten, $"A string of {field} above {GameCatalogCacheFile.MaxStringBytes} bytes should not be written");
    }

    /// <summary>
    /// A string of exactly the bound is written, in bytes and not in characters.
    /// </summary>
    /// <param name="field">The field</param>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow("englishName")]
    [DataRow("englishNameMultiByte")]
    [DataRow("keyFolder")]
    public void GameCatalogCacheFileWriteStringAtBoundReturnsTrue(string field)
    {
        // Arrange
        var (key, catalog) = WithLongString(field, GameCatalogCacheFile.MaxStringBytes);

        // Act
        var isWritten = GameCatalogCacheFile.Write(Stream.Null, key, catalog, EmptyIcons());

        // Assert
        Assert.IsTrue(isWritten, "A string of exactly the bound should be written");
    }

    /// <summary>
    /// A catalog above 64 MiB without the PNG data is not written, one below it is.
    /// </summary>
    /// <param name="entryCount">The number of entries with two long names</param>
    /// <param name="expected">Whether the file is written</param>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow(EntriesBelowBound + 20, false)]
    [DataRow(EntriesBelowBound, true)]
    public void GameCatalogCacheFileWriteCatalogBytesBound(int entryCount, bool expected)
    {
        // Arrange
        var name = new string('a', GameCatalogCacheFile.MaxStringBytes);
        var entries = Enumerable.Range(1, entryCount).Select(guid => new GameCatalogEntry
                                                                     {
                                                                         AssetGuid = guid,
                                                                         Kind = GameCatalogKind.Good,
                                                                         EnglishName = name,
                                                                         GermanName = name
                                                                     }).ToList();

        // Act
        var isWritten = GameCatalogCacheFile.Write(Stream.Null, CacheTestKit.Key(), new GameCatalog(entries, [], 0, 0), EmptyIcons());

        // Assert
        Assert.AreEqual(expected, isWritten, $"The catalog bound of {GameCatalogCacheFile.MaxCatalogBytes} bytes should decide whether the file is written");
    }

    /// <summary>
    /// PNG data above 256 MiB is not written, 250 MiB is.
    /// </summary>
    /// <param name="iconCount">The number of icons of 30 MiB each</param>
    /// <param name="expected">Whether the file is written</param>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow(9, false)]
    [DataRow(8, true)]
    public void GameCatalogCacheFileWritePngBytesBound(int iconCount, bool expected)
    {
        // Arrange
        var data = new byte[Piece + iconCount];
        var icons = Enumerable.Range(0, iconCount).ToDictionary(index => index + 1, index => new GameIcon(1, 1, data.AsMemory(0, Piece + index)));
        var set = new GameIconSet(icons, new Dictionary<int, GameIconProblem>(), 0, 0);

        // Act
        var isWritten = GameCatalogCacheFile.Write(Stream.Null, CacheTestKit.Key(), new GameCatalog([], [], 0, 0), set);

        // Assert
        Assert.AreEqual(expected, isWritten, $"The bound of {GameIconReader.MaxIconSetBytes} bytes of PNG data should decide whether the file is written");
    }

    /// <summary>
    /// Decoded sizes above four times the icon set bound, summed over the distinct icons, are not written; 63 icons of 2,048 pixels
    /// square are, 64 are not.
    /// </summary>
    /// <param name="iconCount">The number of distinct icons</param>
    /// <param name="expected">Whether the file is written</param>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow(64, false)]
    [DataRow(63, true)]
    public void GameCatalogCacheFileWriteDecodedSizeSumBound(int iconCount, bool expected)
    {
        // Arrange
        var icons = Enumerable.Range(0, iconCount).ToDictionary(index => index + 1, index => new GameIcon(GameIconReader.MaxIconSide, GameIconReader.MaxIconSide, new byte[] { (byte)(index + 1) }));
        var set = new GameIconSet(icons, new Dictionary<int, GameIconProblem>(), 0, 0);

        // Act
        var isWritten = GameCatalogCacheFile.Write(Stream.Null, CacheTestKit.Key(), new GameCatalog([], [], 0, 0), set);

        // Assert
        Assert.AreEqual(expected, isWritten, "The decoded sizes summed over the distinct icons should decide whether the file is written");
    }

    #endregion // AC5

    #region AC6

    /// <summary>
    /// A file cut off at any point after the start is invalid.
    /// </summary>
    /// <param name="dropped">The number of bytes cut off at the end</param>
    [TestMethod]
    [TestCategory("AC6")]
    [DataRow(1)]
    [DataRow(31)]
    [DataRow(32)]
    [DataRow(33)]
    [DataRow(100)]
    [DataRow(1000)]
    public void GameCatalogCacheFileReadTruncatedTailReturnsInvalid(int dropped)
    {
        // Arrange
        var bytes = CacheTestKit.WriteSample();
        var cut = bytes[..^dropped];

        // Act
        var result = CacheTestKit.Read(cut, CacheTestKit.Key());

        // Assert
        AssertInvalid(result, $"A file without its last {dropped} bytes should be invalid");
    }

    /// <summary>
    /// A file of only its first bytes is invalid, also an empty one.
    /// </summary>
    /// <param name="kept">The number of bytes kept</param>
    [TestMethod]
    [TestCategory("AC6")]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(7)]
    [DataRow(8)]
    [DataRow(9)]
    [DataRow(12)]
    [DataRow(20)]
    [DataRow(60)]
    public void GameCatalogCacheFileReadTruncatedHeadReturnsInvalid(int kept)
    {
        // Arrange
        var bytes = CacheTestKit.WriteSample();
        var cut = bytes[..kept];

        // Act
        var result = CacheTestKit.Read(cut, CacheTestKit.Key());

        // Assert
        AssertInvalid(result, $"A file of its first {kept} bytes should be invalid");
    }

    /// <summary>
    /// A file cut at the middle of the catalog section and at the middle of the icon section is invalid.
    /// </summary>
    /// <param name="percent">The part of the file that is kept</param>
    [TestMethod]
    [TestCategory("AC6")]
    [DataRow(30)]
    [DataRow(50)]
    [DataRow(75)]
    [DataRow(90)]
    public void GameCatalogCacheFileReadTruncatedInsideSectionsReturnsInvalid(int percent)
    {
        // Arrange
        var bytes = CacheTestKit.WriteSample();
        var cut = bytes[..(bytes.Length * percent / 100)];

        // Act
        var result = CacheTestKit.Read(cut, CacheTestKit.Key());

        // Assert
        AssertInvalid(result, $"A file cut at {percent} percent should be invalid");
    }

    /// <summary>
    /// A flipped byte inside the content or the hash is invalid, because the checksum no longer matches.
    /// </summary>
    /// <param name="fromEnd">The offset of the byte from the end of the file</param>
    [TestMethod]
    [TestCategory("AC6")]
    [DataRow(1)]
    [DataRow(32)]
    [DataRow(33)]
    [DataRow(40)]
    [DataRow(200)]
    public void GameCatalogCacheFileReadFlippedByteReturnsInvalid(int fromEnd)
    {
        // Arrange
        var bytes = CacheTestKit.WriteSample();

        bytes[^fromEnd] ^= 1;

        // Act
        var result = CacheTestKit.Read(bytes, CacheTestKit.Key());

        // Assert
        AssertInvalid(result, $"A file with the byte {fromEnd} from its end flipped should be invalid");
    }

    /// <summary>
    /// A flipped byte in the middle of the icon data is invalid.
    /// </summary>
    [TestMethod]
    [TestCategory("AC6")]
    public void GameCatalogCacheFileReadFlippedMiddleByteReturnsInvalid()
    {
        // Arrange
        var bytes = CacheTestKit.WriteSample();

        bytes[bytes.Length / 2] ^= 1;

        // Act
        var result = CacheTestKit.Read(bytes, CacheTestKit.Key());

        // Assert
        AssertInvalid(result, "A file with a flipped byte in the middle should be invalid");
    }

    /// <summary>
    /// A byte after the hash is invalid.
    /// </summary>
    [TestMethod]
    [TestCategory("AC6")]
    public void GameCatalogCacheFileReadTrailingByteReturnsInvalid()
    {
        // Arrange
        var bytes = CacheTestKit.WriteSample();
        var longer = new byte[bytes.Length + 1];

        bytes.CopyTo(longer, 0);

        // Act
        var result = CacheTestKit.Read(longer, CacheTestKit.Key());

        // Assert
        AssertInvalid(result, "A file with a byte after the hash should be invalid");
    }

    /// <summary>
    /// Another magic is invalid even when the hash fits.
    /// </summary>
    [TestMethod]
    [TestCategory("AC6")]
    public void GameCatalogCacheFileReadWrongMagicReturnsInvalid()
    {
        // Arrange
        var bytes = CacheTestKit.WriteSample();

        bytes[0] ^= 1;
        CacheTestKit.Reseal(bytes);

        // Act
        var result = CacheTestKit.Read(bytes, CacheTestKit.Key());

        // Assert
        AssertInvalid(result, "A file with another magic should be invalid");
    }

    /// <summary>
    /// Another format version is invalid even when the hash fits.
    /// </summary>
    [TestMethod]
    [TestCategory("AC6")]
    public void GameCatalogCacheFileReadOtherFormatVersionReturnsInvalid()
    {
        // Arrange
        var bytes = CacheTestKit.WriteSample();

        Assert.AreEqual(GameCatalogCacheFile.FormatVersion, bytes[8], "The format version should follow the eight bytes of the magic");

        bytes[8] = GameCatalogCacheFile.FormatVersion + 1;
        CacheTestKit.Reseal(bytes);

        // Act
        var result = CacheTestKit.Read(bytes, CacheTestKit.Key());

        // Assert
        AssertInvalid(result, "A file of another format version should be invalid");
    }

    /// <summary>
    /// A length above 320 MiB is refused before the stream is read.
    /// </summary>
    [TestMethod]
    [TestCategory("AC6")]
    public void GameCatalogCacheFileReadLengthAboveMaximumReturnsInvalidUnread()
    {
        // Arrange
        var bytes = CacheTestKit.WriteSample();
        using var stream = new MemoryStream(bytes, false);

        // Act
        var result = GameCatalogCacheFile.Read(stream, GameCatalogCacheFile.MaxFileBytes + 1, CacheTestKit.Key());

        // Assert
        AssertInvalid(result, "A file above the maximum should be invalid");
        Assert.AreEqual(0L, stream.Position, "A file above the maximum should not be read at all");
    }

    /// <summary>
    /// A length that differs from the bytes the stream holds is invalid, whether it is shorter or longer.
    /// </summary>
    /// <param name="difference">The difference of the given length to the real one</param>
    [TestMethod]
    [TestCategory("AC6")]
    [DataRow(-1)]
    [DataRow(10)]
    public void GameCatalogCacheFileReadLengthDiffersFromStreamReturnsInvalid(int difference)
    {
        // Arrange
        var bytes = CacheTestKit.WriteSample();
        using var stream = new MemoryStream(bytes, false);

        // Act
        var result = GameCatalogCacheFile.Read(stream, bytes.Length + difference, CacheTestKit.Key());

        // Assert
        AssertInvalid(result, $"A given length {difference} away from the real one should be invalid");
    }

    /// <summary>
    /// A string one byte above its bound is invalid although the file is complete and its hash fits, and the same string at the
    /// bound is loaded.
    /// </summary>
    [TestMethod]
    [TestCategory("AC6")]
    public void GameCatalogCacheFileReadStringAboveBoundReturnsInvalid()
    {
        // Arrange
        var (key, catalog) = WithLongString("englishName", GameCatalogCacheFile.MaxStringBytes);

        var bytes = CacheTestKit.WriteFile(key, catalog, EmptyIcons());
        var prefix = CacheTestKit.Int32Bytes(GameCatalogCacheFile.MaxStringBytes);
        var start = CacheTestKit.FindOnly(bytes, [.. prefix, .. "aaaaaaaa"u8.ToArray()]);
        var textStart = start + prefix.Length;
        var textEnd = textStart + GameCatalogCacheFile.MaxStringBytes;
        var longer = CacheTestKit.Concat(bytes[..start], CacheTestKit.Int32Bytes(GameCatalogCacheFile.MaxStringBytes + 1), bytes[textStart..textEnd], "a"u8.ToArray(), bytes[textEnd..]);

        CacheTestKit.Reseal(longer);

        // Act
        var result = CacheTestKit.Read(longer, key);
        var control = CacheTestKit.Read(bytes, key);

        // Assert
        AssertInvalid(result, "A string one byte above its bound should be invalid");
        Assert.AreEqual(GameCatalogCacheResultKind.Loaded, control.Kind, "The same string at the bound should be loaded");
    }

    /// <summary>
    /// Bytes that are no UTF-8 in a name are invalid although the hash fits.
    /// </summary>
    [TestMethod]
    [TestCategory("AC6")]
    public void GameCatalogCacheFileReadInvalidUtf8ReturnsInvalid()
    {
        // Arrange
        var bytes = CacheTestKit.WriteSample();
        var start = CacheTestKit.FindOnly(bytes, Encoding.ASCII.GetBytes("Alpha-Name"));

        bytes[start + 1] = 0xFF;
        CacheTestKit.Reseal(bytes);

        // Act
        var result = CacheTestKit.Read(bytes, CacheTestKit.Key());

        // Assert
        AssertInvalid(result, "A name with bytes that are no UTF-8 should be invalid");
    }

    /// <summary>
    /// A kind that is no member of the enumeration is invalid although the hash fits.
    /// </summary>
    [TestMethod]
    [TestCategory("AC6")]
    public void GameCatalogCacheFileReadUndefinedKindReturnsInvalid()
    {
        // Arrange
        var bytes = CacheTestKit.WriteSample();
        var guidStart = CacheTestKit.FindFirst(bytes, CacheTestKit.Int32Bytes(CacheTestKit.GuidAlpha));

        bytes[guidStart + 4] = 0x7F;
        CacheTestKit.Reseal(bytes);

        // Act
        var result = CacheTestKit.Read(bytes, CacheTestKit.Key());

        // Assert
        AssertInvalid(result, "An entry of an undefined kind should be invalid");
    }

    /// <summary>
    /// Two entries of one GUID are invalid.
    /// </summary>
    [TestMethod]
    [TestCategory("AC6")]
    public void GameCatalogCacheFileReadDuplicateGuidReturnsInvalid()
    {
        // Arrange
        var entries = new List<GameCatalogEntry>
                      {
                          new()
                          {
                              AssetGuid = 5,
                              Kind = GameCatalogKind.Good,
                              EnglishName = "A"
                          },
                          new()
                          {
                              AssetGuid = 5,
                              Kind = GameCatalogKind.Good,
                              EnglishName = "B"
                          }
                      };
        var bytes = CacheTestKit.WriteFile(CacheTestKit.Key(), new GameCatalog(entries, [], 0, 0), EmptyIcons());

        // Act
        var result = CacheTestKit.Read(bytes, CacheTestKit.Key());

        // Assert
        AssertInvalid(result, "Two entries of one GUID should be invalid");
    }

    /// <summary>
    /// An entry count that the bytes left cannot hold is invalid, without trying to read that many entries.
    /// </summary>
    /// <param name="row">The row: the largest count, or the number of bytes left</param>
    [TestMethod]
    [TestCategory("AC6")]
    [DataRow("max")]
    [DataRow("bytesLeft")]
    public void GameCatalogCacheFileReadCountAboveBytesLeftReturnsInvalid(string row)
    {
        // Arrange
        var bytes = CacheTestKit.WriteSample();
        var countStart = CacheTestKit.FindOnly(bytes, [.. CacheTestKit.Int32Bytes(3), .. CacheTestKit.Int32Bytes(CacheTestKit.GuidAlpha)]);
        var count = row == "max" ? int.MaxValue : bytes.Length - countStart - 4;

        CacheTestKit.Int32Bytes(count).CopyTo(bytes, countStart);
        CacheTestKit.Reseal(bytes);

        // Act
        var result = CacheTestKit.Read(bytes, CacheTestKit.Key());

        // Assert
        AssertInvalid(result, $"An entry count of {count} should be invalid");
    }

    /// <summary>
    /// An icon that differs from the Companion's own PNG writer in one way is invalid, whatever the way.
    /// </summary>
    /// <param name="row">The row that names the difference</param>
    [TestMethod]
    [TestCategory("AC6")]
    [DataRow("signature")]
    [DataRow("ihdrLength12")]
    [DataRow("ihdrLength14")]
    [DataRow("sideZero")]
    [DataRow("side2049")]
    [DataRow("bitDepth16")]
    [DataRow("colorType2")]
    [DataRow("compression1")]
    [DataRow("filter1")]
    [DataRow("interlace1")]
    [DataRow("sidesDifferFromStored")]
    [DataRow("extraChunkBeforeIdat")]
    [DataRow("extraChunkAfterIdat")]
    [DataRow("twoIdat")]
    [DataRow("idatBeforeIhdr")]
    [DataRow("iendBeforeIdat")]
    [DataRow("crcIhdr")]
    [DataRow("crcIdat")]
    [DataRow("crcIend")]
    [DataRow("iendWithData")]
    [DataRow("byteAfterIend")]
    [DataRow("presetDictionary")]
    [DataRow("brokenDeflate")]
    [DataRow("rowFilter1")]
    [DataRow("rowFilter2")]
    [DataRow("rowFilter3")]
    [DataRow("rowFilter4")]
    [DataRow("decodedShort")]
    [DataRow("decodedLong")]
    [DataRow("bytesAfterZlibEnd")]
    [DataRow("otherCompression")]
    public void GameCatalogCacheFileReadInvalidPngReturnsInvalid(string row)
    {
        // Arrange
        var icon = BrokenPngKit.Build(row);
        var bytes = FileWithIcon(icon);

        // Act
        var result = CacheTestKit.Read(bytes, CacheTestKit.Key());

        // Assert
        AssertInvalid(result, $"An icon with the difference '{row}' should make the file invalid");
    }

    /// <summary>
    /// A PNG built from the parts of the test in the writer's layout is loaded, so the rows of the invalid PNG test differ from it
    /// in the named way only.
    /// </summary>
    [TestMethod]
    [TestCategory("AC6")]
    public void GameCatalogCacheFileReadHandBuiltPngLoads()
    {
        // Arrange
        var rgba = CacheTestKit.Noise(6, 5, 7);
        var png = CacheTestKit.HandBuiltPng(6, 5, rgba);
        var bytes = FileWithIcon(new GameIcon(6, 5, png));

        // Act
        var result = CacheTestKit.Read(bytes, CacheTestKit.Key());

        // Assert
        Assert.AreSequenceEqual(PngWriter.Write(6, 5, rgba), png, "The hand-built PNG should be the writer's output");
        Assert.AreEqual(GameCatalogCacheResultKind.Loaded, result.Kind, "A PNG in the writer's layout should be loaded");
        Assert.AreSequenceEqual(png, result.Icons!.Find(1)!.Png.ToArray(), "The PNG should be loaded unchanged");
    }

    /// <summary>
    /// Icons of the largest side are loaded, so the side bound of the invalid rows is the only reason for their result.
    /// </summary>
    [TestMethod]
    [TestCategory("AC6")]
    public void GameCatalogCacheFileReadLargestSideLoads()
    {
        // Arrange
        var rgba = new byte[GameIconReader.MaxIconSide * 4];
        var bytes = FileWithIcon(new GameIcon(GameIconReader.MaxIconSide, 1, PngWriter.Write(GameIconReader.MaxIconSide, 1, rgba)));

        // Act
        var result = CacheTestKit.Read(bytes, CacheTestKit.Key());

        // Assert
        Assert.AreEqual(GameCatalogCacheResultKind.Loaded, result.Kind, "An icon of 2,048 pixels should be loaded");
    }

    #endregion // AC6

    /// <summary>
    /// Writes a file with one icon for the asset 1 and one good entry.
    /// </summary>
    /// <param name="icon">The icon</param>
    /// <returns>The bytes of the file</returns>
    private static byte[] FileWithIcon(GameIcon icon)
    {
        var catalog = new GameCatalog([
                                          new GameCatalogEntry
                                          {
                                              AssetGuid = 1,
                                              Kind = GameCatalogKind.Good
                                          }
                                      ],
                                      [],
                                      0,
                                      0);
        var icons = new GameIconSet(new Dictionary<int, GameIcon>
                                    {
                                        [1] = icon
                                    },
                                    new Dictionary<int, GameIconProblem>(),
                                    1,
                                    0);

        return CacheTestKit.WriteFile(CacheTestKit.Key(), catalog, icons);
    }

    /// <summary>
    /// Creates an icon set without icons.
    /// </summary>
    /// <returns>The icons</returns>
    private static GameIconSet EmptyIcons()
    {
        return new GameIconSet(new Dictionary<int, GameIcon>(), new Dictionary<int, GameIconProblem>(), 0, 0);
    }

    /// <summary>
    /// Creates a key and a catalog in which one field holds a string of a given size in bytes.
    /// </summary>
    /// <param name="field">The field</param>
    /// <param name="bytes">The size of the string in bytes</param>
    /// <returns>The key and the catalog</returns>
    private static (GameCatalogKey Key, GameCatalog Catalog) WithLongString(string field, int bytes)
    {
        var text = field.EndsWith("MultiByte", StringComparison.Ordinal) ? new string('é', bytes / 2) + new string('a', bytes % 2) : new string('a', bytes);
        var entry = new GameCatalogEntry
                    {
                        AssetGuid = 1,
                        Kind = GameCatalogKind.Good,
                        EnglishName = field.StartsWith("englishName", StringComparison.Ordinal) ? text : null,
                        GermanName = field == "germanName" ? text : null,
                        IconFilename = field == "iconFilename" ? text : null,
                        RegionTypes = field == "regionType" ? [text] : null
                    };
        var key = new GameCatalogKey(field == "keyIdentity" ? text : "i", field == "keyFolder" ? text : "f", field == "keyBuild" ? text : "b", [CacheTestKit.Archive(field == "archivePath" ? text : "a.rda", 1, 0)]);

        return (key, new GameCatalog([entry], [], 0, 0));
    }

    /// <summary>
    /// Creates the expected key of a game that differs from the key of the sample file.
    /// </summary>
    /// <param name="difference">The difference</param>
    /// <returns>The key</returns>
    private static GameCatalogKey ChangedGame(string difference)
    {
        var archive = CacheTestKit.Archive("a.rda", 7, 0);

        return difference switch
               {
                   "build" => new GameCatalogKey("idA", "f", "b2", [archive]),
                   "buildUnknown" => new GameCatalogKey("idA", "f", null, [archive]),
                   "folder" => new GameCatalogKey("idA", "g", "b1", [archive]),
                   "length" => new GameCatalogKey("idA", "f", "b1", [CacheTestKit.Archive("a.rda", 8, 0)]),
                   "time" => new GameCatalogKey("idA", "f", "b1", [CacheTestKit.Archive("a.rda", 7, 1)]),
                   "added" => new GameCatalogKey("idA", "f", "b1", [archive, CacheTestKit.Archive("b.rda", 1, 0)]),
                   "removed" => new GameCatalogKey("idA", "f", "b1", []),
                   _ => throw new ArgumentException("Unknown difference " + difference, nameof(difference))
               };
    }

    /// <summary>
    /// Asserts that a result is invalid and holds nothing.
    /// </summary>
    /// <param name="result">The result</param>
    /// <param name="message">The message</param>
    private static void AssertInvalid(GameCatalogCacheResult result, string message)
    {
        AssertNothingLoaded(result, GameCatalogCacheResultKind.Invalid, message);
    }

    /// <summary>
    /// Asserts the kind of a result that holds neither catalog nor icons.
    /// </summary>
    /// <param name="result">The result</param>
    /// <param name="kind">The expected kind</param>
    /// <param name="message">The message</param>
    private static void AssertNothingLoaded(GameCatalogCacheResult result, GameCatalogCacheResultKind kind, string message)
    {
        Assert.AreEqual(kind, result.Kind, message);
        Assert.IsNull(result.Catalog, message + ": no catalog should be returned");
        Assert.IsNull(result.Icons, message + ": no icons should be returned");
    }

    #endregion // Methods
}