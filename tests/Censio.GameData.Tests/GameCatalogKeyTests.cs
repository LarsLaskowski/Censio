namespace Censio.GameData.Tests;

/// <summary>
/// Tests for <see cref="GameCatalogKey" />.
/// </summary>
[TestClass]
public sealed class GameCatalogKeyTests
{
    #region Constants

    /// <summary>
    /// The folder of the base key.
    /// </summary>
    private const string Folder = "C:\\Games\\Anno";

    #endregion // Constants

    #region Methods

    /// <summary>
    /// Keys of the same folder, build and archives are the same game, whatever the Companion identity.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void GameCatalogKeyIsSameGameEqualStateIgnoresIdentity()
    {
        // Arrange
        var first = Key("identity-one", "C:\\Games\\Anno", "1.2", Archives());
        var second = Key("identity-two", "C:\\Games\\Anno", "1.2", Archives());

        // Act
        var isSame = first.IsSameGame(second);

        // Assert
        Assert.IsTrue(isSame, "A different Companion identity should not make the game differ");
    }

    /// <summary>
    /// A build that is unknown on both sides is equal to itself.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void GameCatalogKeyIsSameGameUnknownBuildEqualsUnknownBuild()
    {
        // Arrange
        var first = Key("i", "C:\\Games\\Anno", null, Archives());
        var second = Key("i", "C:\\Games\\Anno", null, Archives());

        // Act
        var isSame = first.IsSameGame(second);

        // Assert
        Assert.IsTrue(isSame, "An unknown build should equal an unknown build");
    }

    /// <summary>
    /// The case of the folder and of an archive path is ignored.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void GameCatalogKeyIsSameGameIgnoresCaseOfFolderAndPath()
    {
        // Arrange
        var first = Key("i", "C:\\Games\\Anno", "1", [CacheTestKit.Archive("Sub\\Data.rda", 5, 0)]);
        var second = Key("i", "c:\\games\\ANNO", "1", [CacheTestKit.Archive("sub\\data.RDA", 5, 0)]);

        // Act
        var isSame = first.IsSameGame(second);

        // Assert
        Assert.IsTrue(isSame, "The case of the folder and of a path should be ignored");
    }

    /// <summary>
    /// Each difference of the game state makes the keys differ.
    /// </summary>
    /// <param name="difference">The difference</param>
    [TestMethod]
    [TestCategory("AC2")]
    [DataRow("build")]
    [DataRow("buildBecomesUnknown")]
    [DataRow("added")]
    [DataRow("removed")]
    [DataRow("length")]
    [DataRow("time")]
    [DataRow("timeByOneTick")]
    [DataRow("folder")]
    [DataRow("path")]
    [DataRow("order")]
    public void GameCatalogKeyIsSameGameDiffers(string difference)
    {
        // Arrange
        var first = Key("i", "C:\\Games\\Anno", "1.2", Archives());
        var second = Changed(difference);

        // Act
        var isSame = first.IsSameGame(second);
        var isSameBack = second.IsSameGame(first);

        // Assert
        Assert.IsFalse(isSame, $"The change '{difference}' should make the game differ");
        Assert.IsFalse(isSameBack, $"The comparison should be symmetric for the change '{difference}'");
    }

    /// <summary>
    /// Creates a key.
    /// </summary>
    /// <param name="identity">The identity</param>
    /// <param name="folder">The folder</param>
    /// <param name="build">The build</param>
    /// <param name="archives">The archives</param>
    /// <returns>The key</returns>
    private static GameCatalogKey Key(string identity, string folder, string? build, IReadOnlyList<GameArchiveState> archives)
    {
        return new GameCatalogKey(identity, folder, build, archives);
    }

    /// <summary>
    /// Creates the archives of the base key.
    /// </summary>
    /// <returns>The archives</returns>
    private static List<GameArchiveState> Archives()
    {
        return [CacheTestKit.Archive("a.rda", 10, 0), CacheTestKit.Archive("b.rda", 20, 5)];
    }

    /// <summary>
    /// Creates the key of a difference from the base key.
    /// </summary>
    /// <param name="difference">The difference</param>
    /// <returns>The key</returns>
    private static GameCatalogKey Changed(string difference)
    {
        return difference switch
               {
                   "build" => Key("i", Folder, "1.3", Archives()),
                   "buildBecomesUnknown" => Key("i", Folder, null, Archives()),
                   "added" => Key("i", Folder, "1.2", [.. Archives(), CacheTestKit.Archive("c.rda", 1, 0)]),
                   "removed" => Key("i", Folder, "1.2", Archives()[..1]),
                   "length" => Key("i", Folder, "1.2", [CacheTestKit.Archive("a.rda", 11, 0), CacheTestKit.Archive("b.rda", 20, 5)]),
                   "time" => Key("i", Folder, "1.2", [CacheTestKit.Archive("a.rda", 10, 0), CacheTestKit.Archive("b.rda", 20, 6)]),
                   "timeByOneTick" => Key("i",
                                          Folder,
                                          "1.2",
                                          [
                                              CacheTestKit.Archive("a.rda", 10, 0),
                                              new GameArchiveState
                                              {
                                                  Path = "b.rda",
                                                  Length = 20,
                                                  LastWriteTimeUtc = CacheTestKit.Archive("b.rda", 20, 5).LastWriteTimeUtc.AddTicks(1)
                                              }
                                          ]),
                   "folder" => Key("i", "C:\\Games\\Other", "1.2", Archives()),
                   "path" => Key("i", Folder, "1.2", [CacheTestKit.Archive("a.rda", 10, 0), CacheTestKit.Archive("x.rda", 20, 5)]),
                   "order" => Key("i", Folder, "1.2", [.. Archives().AsEnumerable().Reverse()]),
                   _ => throw new ArgumentException("Unknown difference " + difference, nameof(difference))
               };
    }

    #endregion // Methods
}