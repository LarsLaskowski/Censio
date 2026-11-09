namespace Censio.GameData;

/// <summary>
/// Builds the game catalog from the configuration files of an installation.
/// </summary>
public static class GameCatalogReader
{
    #region Constants

    /// <summary>
    /// The deepest nesting of a defining asset
    /// </summary>
    public const int MaxAssetDepth = 256;

    /// <summary>
    /// The most elements of a defining asset or text
    /// </summary>
    public const int MaxAssetElements = 65536;

    /// <summary>
    /// The archive path of the assets file
    /// </summary>
    public const string AssetsPath = "data/base/config/export/assets.xml";

    /// <summary>
    /// The archive path of the data sets file
    /// </summary>
    public const string DataSetsPath = "data/base/config/game/datasets.xml";

    /// <summary>
    /// The archive path of the English texts file
    /// </summary>
    public const string EnglishTextsPath = "data/base/config/gui/texts_english.xml";

    /// <summary>
    /// The archive path of the German texts file
    /// </summary>
    public const string GermanTextsPath = "data/base/config/gui/texts_german.xml";

    #endregion // Constants

    #region Methods

    /// <summary>
    /// Builds the catalog from four sources, each opened when its pass starts.
    /// </summary>
    /// <param name="openDataSets">Opens the data sets file</param>
    /// <param name="openAssets">Opens the assets file</param>
    /// <param name="openEnglishTexts">Opens the English texts file</param>
    /// <param name="openGermanTexts">Opens the German texts file</param>
    /// <returns>The result</returns>
    public static GameCatalogResult Read(Func<GameCatalogSource> openDataSets, Func<GameCatalogSource> openAssets, Func<GameCatalogSource> openEnglishTexts, Func<GameCatalogSource> openGermanTexts)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Builds the catalog from the archives of an installation.
    /// </summary>
    /// <param name="installationFolder">The installation folder</param>
    /// <param name="limits">The archive limits</param>
    /// <returns>The result</returns>
    public static GameCatalogResult Load(string installationFolder, RdaLimits limits)
    {
        throw new NotImplementedException();
    }

    #endregion // Methods
}