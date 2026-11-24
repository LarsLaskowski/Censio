global using CatalogData = (Censio.GameData.GameCatalog Catalog, Censio.GameData.GameIconSet? Icons, string Build, string Identity, string[] References, byte[][] Pngs);

using System.Security.Cryptography;

using Censio.Contracts;
using Censio.GameData;

namespace Censio.Companion.Core.Tests;

/// <summary>
/// Builds the catalogs the catalog upload tests send: a region, goods with distinct icons, the document the converter and the
/// document writer give for them, its identity and the references of its icons.
/// </summary>
internal static class CatalogTestData
{
    #region Constants

    /// <summary>
    /// The build of the catalogs, four numbers as the catalog document requires.
    /// </summary>
    public const string Build = "1.2.3.4";

    #endregion // Constants

    #region Methods

    /// <summary>
    /// Creates the catalog of a name: one region and the given number of goods, each good with an icon of its own.
    /// </summary>
    /// <param name="name">The name; catalogs of different names differ in names, icons and identity</param>
    /// <param name="iconCount">The number of goods, each with an icon</param>
    /// <param name="hasIcons">Whether the catalog has an icon set; without one it has none, as after failed icons</param>
    /// <returns>The catalog, its icons or <c>null</c>, the build, the identity, the references and the PNG data of the icons</returns>
    public static CatalogData Create(string name, int iconCount, bool hasIcons = true)
    {
        var entries = new List<GameCatalogEntry>
                      {
                          new()
                          {
                              AssetGuid = 100,
                              Kind = GameCatalogKind.Region,
                              EnglishName = $"Region {name}",
                              GermanName = $"Region {name} DE",
                              RegionGuid = 5,
                              RegionTypes = ["Coast"]
                          }
                      };
        var pngs = new byte[iconCount][];
        var icons = new Dictionary<int, GameIcon>();

        for (var index = 0; index < iconCount; index++)
        {
            var pixels = new byte[16];

            pixels[0] = (byte)name[0];
            pixels[1] = (byte)(index + 1);
            pngs[index] = PngWriter.Write(2, 2, pixels);
            icons[index + 1] = new GameIcon(2, 2, pngs[index]);
            entries.Add(new GameCatalogEntry
                        {
                            AssetGuid = index + 1,
                            Kind = GameCatalogKind.Good,
                            EnglishName = $"Good {name} {index + 1}",
                            GermanName = $"Ware {name} {index + 1}"
                        });
        }

        var catalog = new GameCatalog(entries, [], 0, 0);
        var iconSet = hasIcons ? new GameIconSet(icons, new Dictionary<int, GameIconProblem>(), iconCount, 0) : null;
        var references = hasIcons ? pngs.Select(png => Convert.ToHexStringLower(SHA256.HashData(png))).ToArray() : [];

        return (catalog, iconSet, Build, IdentityOf(catalog, iconSet, Build), references, pngs);
    }

    /// <summary>
    /// Writes the catalog document of a catalog as the Companion does.
    /// </summary>
    /// <param name="catalog">The catalog</param>
    /// <param name="icons">The icons, or <c>null</c></param>
    /// <param name="build">The build</param>
    /// <returns>The bytes of the document</returns>
    public static byte[] WriteDocument(GameCatalog catalog, GameIconSet? icons, string build)
    {
        var written = CatalogDocumentWriter.Write(GameCatalogDocumentConverter.ToDocument(catalog, icons, build));

        Assert.IsNull(written.Error, "The document of the test catalog should be written");

        return written.Utf8Json.ToArray();
    }

    /// <summary>
    /// Gets the names a catalog of a name holds, as they must never reach the log.
    /// </summary>
    /// <param name="name">The name</param>
    /// <param name="iconCount">The number of goods</param>
    /// <returns>The English and German names of every entry</returns>
    public static string[] NamesOf(string name, int iconCount)
    {
        return
        [
            $"Region {name}",
            $"Region {name} DE",
            .. Enumerable.Range(1, iconCount).SelectMany(index => new[] { $"Good {name} {index}", $"Ware {name} {index}" })
        ];
    }

    /// <summary>
    /// Computes the identity of a catalog: the SHA-256 of its document's bytes.
    /// </summary>
    /// <param name="catalog">The catalog</param>
    /// <param name="icons">The icons, or <c>null</c></param>
    /// <param name="build">The build</param>
    /// <returns>The identity in lowercase hex</returns>
    private static string IdentityOf(GameCatalog catalog, GameIconSet? icons, string build)
    {
        return Convert.ToHexStringLower(SHA256.HashData(WriteDocument(catalog, icons, build)));
    }

    #endregion // Methods
}