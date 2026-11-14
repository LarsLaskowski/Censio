using System.Text;

namespace Censio.GameData.Tests;

/// <summary>
/// Tests for the limits and the invalid values <see cref="GameCatalogCacheFile" /> refuses when it reads a file: the file is
/// made from a written one by changing the bytes at a place counted from the icon section or from the end, and, where only
/// that change may refuse it, writing the hash again; the tests assume the layout of record 0053.
/// </summary>
[TestClass]
public sealed class GameCatalogCacheFileReadLimitsTests
{
    #region Constants

    /// <summary>
    /// The size of the string of an archive path of the catalog bound test.
    /// </summary>
    private const int PathBytes = 65536;

    /// <summary>
    /// The size in bytes of one archive of the catalog bound test: path length, path, length and time.
    /// </summary>
    private const int ArchiveBytes = 4 + PathBytes + 16;

    /// <summary>
    /// The number of archives that hold more than the bound of the catalog.
    /// </summary>
    private const int ArchivesAboveBound = 1030;

    /// <summary>
    /// The size of a stream that holds more than the bound of the PNG data.
    /// </summary>
    private const long LargeStream = 314572800;

    /// <summary>
    /// The size of the hash at the end of a file.
    /// </summary>
    private const int HashBytes = 32;

    /// <summary>
    /// The size of the assignment of an icon or a problem to a GUID.
    /// </summary>
    private const int AssignmentBytes = 9;

    #endregion // Constants

    #region Methods

    #region AC6

    /// <summary>
    /// The file of the tests loads when nothing is changed, so the rows of the other tests differ from it in the named way only.
    /// </summary>
    [TestMethod]
    [TestCategory("AC6")]
    public void GameCatalogCacheFileReadAssignmentFileLoads()
    {
        // Arrange
        var bytes = AssignmentFile();

        // Act
        var result = CacheTestKit.Read(bytes, CacheTestKit.Key());

        // Assert
        Assert.AreEqual(GameCatalogCacheResultKind.Loaded, result.Kind, "The unchanged file should be loaded");
        Assert.HasCount(2, result.Icons!.Icons, "Both icons should be assigned");
        Assert.HasCount(1, result.Icons.Problems, "The problem should be assigned");
    }

    /// <summary>
    /// A value that no writer produces is invalid although the file is complete and its hash fits.
    /// </summary>
    /// <param name="row">The row that names the value</param>
    [TestMethod]
    [TestCategory("AC6")]
    [DataRow("unusedPng")]
    [DataRow("indexTooLarge")]
    [DataRow("indexNegative")]
    [DataRow("duplicateIconGuid")]
    [DataRow("undefinedTag")]
    [DataRow("problemUndefined")]
    [DataRow("problemNegative")]
    [DataRow("goodsNegative")]
    [DataRow("regionsNegative")]
    [DataRow("assignmentCountAboveBytes")]
    [DataRow("iconCountNegative")]
    [DataRow("pngLengthZero")]
    [DataRow("pngLengthAboveBytesLeft")]
    [DataRow("inheritedNegative")]
    [DataRow("droppedNegative")]
    [DataRow("ambiguousCountNegative")]
    [DataRow("flagAbove1")]
    [DataRow("identityLengthNegative")]
    [DataRow("archivePathLengthNegative")]
    public void GameCatalogCacheFileReadInvalidValueReturnsInvalid(string row)
    {
        // Arrange
        var bytes = AssignmentFile();

        Change(bytes, row);
        CacheTestKit.Reseal(bytes);

        // Act
        var result = CacheTestKit.Read(bytes, CacheTestKit.Key());

        // Assert
        AssertInvalid(result, $"A file with the value '{row}' should be invalid");
    }

    /// <summary>
    /// A stream that fails with an <see cref="IOException" /> while the file is read gives an invalid result and no exception,
    /// wherever it fails: in a string, in PNG data or at the hash.
    /// </summary>
    /// <param name="place">The place of the failure</param>
    [TestMethod]
    [TestCategory("AC6")]
    [DataRow("identityText")]
    [DataRow("pngData")]
    [DataRow("hash")]
    public void GameCatalogCacheFileReadStreamFailureReturnsInvalid(string place)
    {
        // Arrange
        var bytes = AssignmentFile();
        var offset = place switch
                     {
                         "identityText" => 16,
                         "pngData" => FirstPngStart(bytes) + 2,
                         _ => bytes.Length - (HashBytes / 2)
                     };
        using var stream = new SparseStream(bytes.Length).WithRegion(0, bytes).ThrowingAt(offset);

        // Act
        var result = GameCatalogCacheFile.Read(stream, bytes.Length, CacheTestKit.Key());

        // Assert
        AssertInvalid(result, $"A stream that fails in '{place}' should give an invalid result");
    }

    /// <summary>
    /// The same bytes from a stream that does not fail are loaded, so the stream failures of the test above are the reason for
    /// their result.
    /// </summary>
    [TestMethod]
    [TestCategory("AC6")]
    public void GameCatalogCacheFileReadSparseStreamWithoutFailureLoads()
    {
        // Arrange
        var bytes = AssignmentFile();
        using var stream = new SparseStream(bytes.Length).WithRegion(0, bytes);

        // Act
        var result = GameCatalogCacheFile.Read(stream, bytes.Length, CacheTestKit.Key());

        // Assert
        Assert.AreEqual(GameCatalogCacheResultKind.Loaded, result.Kind, "A stream that does not fail should load the file");
    }

    /// <summary>
    /// More than 64 MiB of everything but the PNG data stops the reading at the bound, before the rest of the file is read.
    /// </summary>
    [TestMethod]
    [TestCategory("AC6")]
    public void GameCatalogCacheFileReadCatalogAboveBoundStopsAtBound()
    {
        // Arrange
        var header = CacheTestKit.Concat("CENSIOCC"u8.ToArray(),
                                         CacheTestKit.Int32Bytes(GameCatalogCacheFile.FormatVersion),
                                         CacheTestKit.Int32Bytes(1),
                                         "i"u8.ToArray(),
                                         CacheTestKit.Int32Bytes(1),
                                         "f"u8.ToArray(),
                                         [0],
                                         CacheTestKit.Int32Bytes(ArchivesAboveBound));
        var length = header.Length + ((long)ArchivesAboveBound * ArchiveBytes) + 1048576 + HashBytes;
        using var stream = new SparseStream(length).WithRegion(0, header);
        var path = new string('\0', PathBytes);
        var archives = new List<GameArchiveState>();

        for (var index = 0; index < ArchivesAboveBound; index++)
        {
            stream.WithRegion(header.Length + ((long)index * ArchiveBytes), CacheTestKit.Int32Bytes(PathBytes));
            archives.Add(new GameArchiveState
                         {
                             Path = path,
                             Length = 0,
                             LastWriteTimeUtc = new DateTime(0, DateTimeKind.Utc)
                         });
        }

        // Act
        var result = GameCatalogCacheFile.Read(stream, length, new GameCatalogKey("i", "f", null, archives));

        // Assert
        AssertInvalid(result, "A file with more than 64 MiB besides the PNG data should be invalid");
        Assert.IsLessThanOrEqualTo(GameCatalogCacheFile.MaxCatalogBytes, stream.Position, "The reading should stop at the bound of the catalog");
        Assert.IsGreaterThan(GameCatalogCacheFile.MaxCatalogBytes - (2L * ArchiveBytes), stream.Position, "The reading should reach the bound before it stops");
    }

    /// <summary>
    /// A PNG length that lets the PNG data exceed 256 MiB is invalid, and the data is not read.
    /// </summary>
    [TestMethod]
    [TestCategory("AC6")]
    public void GameCatalogCacheFileReadPngDataAboveBoundStopsBeforeData()
    {
        // Arrange
        var bytes = AssignmentFile();
        var start = FirstPngStart(bytes);

        CacheTestKit.Int32Bytes(GameIconReader.MaxIconSetBytes + 1).CopyTo(bytes, start - 4);

        using var stream = new SparseStream(LargeStream).WithRegion(0, bytes);

        // Act
        var result = GameCatalogCacheFile.Read(stream, LargeStream, CacheTestKit.Key());

        // Assert
        AssertInvalid(result, "A PNG length above 256 MiB should be invalid");
        Assert.IsLessThanOrEqualTo(start, stream.Position, "The PNG data above the bound should not be read");
    }

    /// <summary>
    /// A stored size whose decoded size alone is above four times the icon set bound is invalid, and the PNG data is not read.
    /// </summary>
    [TestMethod]
    [TestCategory("AC6")]
    public void GameCatalogCacheFileReadDecodedSizeAboveBoundStopsBeforeData()
    {
        // Arrange
        var bytes = AssignmentFile();
        var start = FirstPngStart(bytes);

        CacheTestKit.Int32Bytes(100_000_000).CopyTo(bytes, start - 12);
        CacheTestKit.Reseal(bytes);

        using var stream = new MemoryStream(bytes, false);

        // Act
        var result = GameCatalogCacheFile.Read(stream, bytes.Length, CacheTestKit.Key());

        // Assert
        AssertInvalid(result, "A decoded size above four times the icon set bound should be invalid");
        Assert.IsLessThanOrEqualTo(start, stream.Position, "The PNG data of an icon above the bound should not be read");
    }

    /// <summary>
    /// A loaded result cannot be created without content.
    /// </summary>
    [TestMethod]
    [TestCategory("AC6")]
    public void GameCatalogCacheResultFromKindLoadedThrows()
    {
        // Act and Assert
        Assert.ThrowsExactly<ArgumentException>(() => GameCatalogCacheResult.FromKind(GameCatalogCacheResultKind.Loaded), "A loaded result without catalog and icons should be refused");
    }

    #endregion // AC6

    #region AC5

    /// <summary>
    /// A string that is no valid Unicode is not written, wherever it is, and nothing is written after it, PNG data included.
    /// </summary>
    /// <param name="field">The field that holds the string</param>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow("englishName")]
    [DataRow("regionType")]
    [DataRow("keyIdentity")]
    public void GameCatalogCacheFileWriteLoneSurrogateReturnsFalseAndWritesNoIcons(string field)
    {
        // Arrange
        var broken = "a\uD800b";

        var icons = CacheTestKit.SampleIcons();
        var entry = new GameCatalogEntry
                    {
                        AssetGuid = 1,
                        Kind = GameCatalogKind.Good,
                        EnglishName = field == "englishName" ? broken : "x",
                        RegionTypes = field == "regionType" ? [broken] : null
                    };
        var key = CacheTestKit.Key(field == "keyIdentity" ? broken : "idA");
        using var stream = new MemoryStream();

        // Act
        var isWritten = GameCatalogCacheFile.Write(stream, key, new GameCatalog([entry], [], 0, 0), icons);

        // Assert
        Assert.IsFalse(isWritten, $"A lone surrogate in {field} should not be written");
        Assert.AreEqual(-1, stream.ToArray().AsSpan().IndexOf(icons.Find(CacheTestKit.GuidAlpha)!.Png.Span), "No PNG data should be written after the failure");
    }

    #endregion // AC5

    /// <summary>
    /// Writes a file with three entries, two distinct icons for the assets 1 and 2 and the problem of the asset 3.
    /// </summary>
    /// <returns>The bytes of the file</returns>
    private static byte[] AssignmentFile()
    {
        var entries = Enumerable.Range(1, 3).Select(guid => new GameCatalogEntry
                                                            {
                                                                AssetGuid = guid,
                                                                Kind = GameCatalogKind.Good
                                                            }).ToList();
        var icons = new GameIconSet(new Dictionary<int, GameIcon>
                                    {
                                        [1] = CacheTestKit.NoiseIcon(4, 4, 1),
                                        [2] = CacheTestKit.NoiseIcon(5, 3, 2)
                                    },
                                    new Dictionary<int, GameIconProblem>
                                    {
                                        [3] = GameIconProblem.Missing
                                    },
                                    2,
                                    0);

        return CacheTestKit.WriteFile(CacheTestKit.Key(), new GameCatalog(entries, [], 0, 0), icons);
    }

    /// <summary>
    /// Finds the offset of the PNG data of the first distinct icon.
    /// </summary>
    /// <param name="bytes">The bytes of the file of <see cref="AssignmentFile" /></param>
    /// <returns>The offset</returns>
    private static int FirstPngStart(byte[] bytes)
    {
        return CacheTestKit.FindOnly(bytes, CacheTestKit.NoiseIcon(4, 4, 1).Png.ToArray());
    }

    /// <summary>
    /// Changes the bytes of the file of <see cref="AssignmentFile" /> as a row names.
    /// </summary>
    /// <param name="bytes">The bytes, changed in place</param>
    /// <param name="row">The row</param>
    private static void Change(byte[] bytes, string row)
    {
        var hashStart = bytes.Length - HashBytes;
        var pngStart = FirstPngStart(bytes);
        var second = hashStart - 8 - (2 * AssignmentBytes);
        var third = hashStart - 8 - AssignmentBytes;

        switch (row)
        {
            case "unusedPng":
                Put(bytes, second + 5, 0);

                break;

            case "indexTooLarge":
                Put(bytes, second + 5, 2);

                break;

            case "indexNegative":
                Put(bytes, second + 5, -1);

                break;

            case "duplicateIconGuid":
                Put(bytes, second, 1);

                break;

            case "undefinedTag":
                bytes[second + 4] = 2;

                break;

            case "problemUndefined":
                Put(bytes, third + 5, 999);

                break;

            case "problemNegative":
                Put(bytes, third + 5, -1);

                break;

            case "goodsNegative":
                Put(bytes, hashStart - 8, -1);

                break;

            case "regionsNegative":
                Put(bytes, hashStart - 4, -1);

                break;

            case "assignmentCountAboveBytes":
                Put(bytes, hashStart - 8 - (3 * AssignmentBytes) - 4, int.MaxValue);

                break;

            case "iconCountNegative":
                Put(bytes, pngStart - 16, -1);

                break;

            case "pngLengthZero":
                Put(bytes, pngStart - 4, 0);

                break;

            case "pngLengthAboveBytesLeft":
                Put(bytes, pngStart - 4, int.MaxValue);

                break;

            case "inheritedNegative":
                Put(bytes, pngStart - 24, -1);

                break;

            case "droppedNegative":
                Put(bytes, pngStart - 20, -1);

                break;

            case "ambiguousCountNegative":
                Put(bytes, pngStart - 28, -1);

                break;

            case "flagAbove1":
                bytes[CacheTestKit.FindOnly(bytes, [.. CacheTestKit.Int32Bytes(2), .. "b1"u8.ToArray()]) - 1] = 2;

                break;

            case "identityLengthNegative":
                Put(bytes, 12, -1);

                break;

            case "archivePathLengthNegative":
                Put(bytes, CacheTestKit.FindOnly(bytes, [.. CacheTestKit.Int32Bytes(5), .. Encoding.ASCII.GetBytes("a.rda")]), -1);

                break;

            default:
                throw new ArgumentException("Unknown row " + row, nameof(row));
        }
    }

    /// <summary>
    /// Writes an integer into the bytes.
    /// </summary>
    /// <param name="bytes">The bytes, changed in place</param>
    /// <param name="offset">The offset</param>
    /// <param name="value">The value</param>
    private static void Put(byte[] bytes, int offset, int value)
    {
        CacheTestKit.Int32Bytes(value).CopyTo(bytes, offset);
    }

    /// <summary>
    /// Asserts that a result is invalid and holds nothing.
    /// </summary>
    /// <param name="result">The result</param>
    /// <param name="message">The message</param>
    private static void AssertInvalid(GameCatalogCacheResult result, string message)
    {
        Assert.AreEqual(GameCatalogCacheResultKind.Invalid, result.Kind, message);
        Assert.IsNull(result.Catalog, message + ": no catalog should be returned");
        Assert.IsNull(result.Icons, message + ": no icons should be returned");
    }

    #endregion // Methods
}