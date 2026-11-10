using System.Runtime.InteropServices;
using System.Xml;

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

    /// <summary>
    /// The archive path of the property defaults file
    /// </summary>
    public const string PropertiesPath = "data/base/config/export/properties.xml";

    /// <summary>
    /// The archive path of the templates file
    /// </summary>
    public const string TemplatesPath = "data/base/config/export/templates.xml";

    /// <summary>
    /// The name of the configuration archive in <c>maindata</c>
    /// </summary>
    private const string ConfigArchiveName = "config.rda";

    #endregion // Constants

    #region Methods

    /// <summary>
    /// Builds the catalog from six sources, each opened when its pass starts.
    /// </summary>
    /// <param name="openDataSets">Opens the data sets file</param>
    /// <param name="openAssets">Opens the assets file</param>
    /// <param name="openEnglishTexts">Opens the English texts file</param>
    /// <param name="openGermanTexts">Opens the German texts file</param>
    /// <param name="openProperties">Opens the property defaults file</param>
    /// <param name="openTemplates">Opens the templates file</param>
    /// <returns>The result</returns>
    public static GameCatalogResult Read(Func<GameCatalogSource> openDataSets, Func<GameCatalogSource> openAssets, Func<GameCatalogSource> openEnglishTexts, Func<GameCatalogSource> openGermanTexts, Func<GameCatalogSource> openProperties, Func<GameCatalogSource> openTemplates)
    {
        ArgumentNullException.ThrowIfNull(openDataSets);
        ArgumentNullException.ThrowIfNull(openAssets);
        ArgumentNullException.ThrowIfNull(openEnglishTexts);
        ArgumentNullException.ThrowIfNull(openGermanTexts);
        ArgumentNullException.ThrowIfNull(openProperties);
        ArgumentNullException.ThrowIfNull(openTemplates);

        try
        {
            if (TryReadPass(openDataSets, GameCatalogFiles.ReadRegionTypes, out var regionTypes, out var failure) == false
                || TryReadPass(openProperties, GameCatalogDefaults.ReadPropertyDefaults, out var defaults, out failure) == false
                || TryReadPass(openTemplates, GameCatalogDefaults.ReadTemplates, out var templates, out failure) == false
                || TryReadPass(openAssets, stream => GameCatalogFiles.ReadAssets(stream, templates, defaults), out var assets, out failure) == false)
            {
                return CreateError(failure);
            }

            var used = GameCatalogBuilder.UsedTextIds(assets);

            if (TryReadPass(openEnglishTexts, stream => GameCatalogFiles.ReadTexts(stream, used), out var english, out failure) == false
                || TryReadPass(openGermanTexts, stream => GameCatalogFiles.ReadTexts(stream, used), out var german, out failure) == false)
            {
                return CreateError(failure);
            }

            return GameCatalogResult.FromCatalog(GameCatalogBuilder.Build(regionTypes, assets, english, german), [], 0);
        }
        catch (XmlException)
        {
            return CreateError(GameCatalogErrorKind.MalformedFile);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return CreateError(GameCatalogErrorKind.FileUnreadable);
        }
    }

    /// <summary>
    /// Builds the catalog from the archives of an installation.
    /// </summary>
    /// <param name="installationFolder">The installation folder</param>
    /// <param name="limits">The archive limits</param>
    /// <returns>The result</returns>
    public static GameCatalogResult Load(string installationFolder, RdaLimits limits)
    {
        ArgumentNullException.ThrowIfNull(installationFolder);
        ArgumentNullException.ThrowIfNull(limits);

        try
        {
            var mainData = Path.Combine(installationFolder, "maindata");
            var opened = RdaArchive.Open(Path.Combine(mainData, ConfigArchiveName), limits);

            if (opened.Archive is null)
            {
                return CreateError(GameCatalogErrorKind.ArchiveUnreadable);
            }

            using var archive = opened.Archive;

            var result = Read(() => OpenEntry(archive, DataSetsPath), () => OpenEntry(archive, AssetsPath), () => OpenEntry(archive, EnglishTextsPath), () => OpenEntry(archive, GermanTextsPath), () => OpenEntry(archive, PropertiesPath), () => OpenEntry(archive, TemplatesPath));

            if (result.Catalog is null)
            {
                return result;
            }

            var copies = FindCopies(mainData, limits, out var uncheckedCount);

            return GameCatalogResult.FromCatalog(result.Catalog, copies, uncheckedCount);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return CreateError(GameCatalogErrorKind.ArchiveUnreadable);
        }
    }

    /// <summary>
    /// Opens a source and reads it in one pass; the stream is disposed when the pass ends.
    /// </summary>
    /// <typeparam name="TResult">The type of what the pass reads</typeparam>
    /// <param name="open">Opens the source</param>
    /// <param name="read">Reads the stream</param>
    /// <param name="result">What the pass read</param>
    /// <param name="failure">The kind of the source's error when the source could not be opened</param>
    /// <returns><c>true</c> when the pass read the source</returns>
    private static bool TryReadPass<TResult>(Func<GameCatalogSource> open, Func<Stream, TResult> read, out TResult result, out GameCatalogErrorKind failure)
    {
        GameCatalogSource source;

        try
        {
            source = open();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            throw new IOException();
        }

        result = default!;
        failure = source.ErrorKind ?? GameCatalogErrorKind.FileUnreadable;

        if (source.Stream is not { } stream)
        {
            return false;
        }

        using (stream)
        {
            result = read(stream);
        }

        return true;
    }

    /// <summary>
    /// Opens the content of an archive entry as a source.
    /// </summary>
    /// <param name="archive">The archive</param>
    /// <param name="path">The path of the entry</param>
    /// <returns>The source</returns>
    private static GameCatalogSource OpenEntry(RdaArchive archive, string path)
    {
        var result = archive.Read(path);

        if (result.Error is { } error)
        {
            return GameCatalogSource.FromError(error.Kind == RdaErrorKind.NotFound ? GameCatalogErrorKind.FileMissing : GameCatalogErrorKind.FileUnreadable);
        }

        var content = result.Content;

        return GameCatalogSource.FromStream(MemoryMarshal.TryGetArray(content, out var segment) && segment.Array is not null
                                                ? new MemoryStream(segment.Array, segment.Offset, segment.Count, false)
                                                : new MemoryStream(content.ToArray(), false));
    }

    /// <summary>
    /// Finds the catalog files that other archives list.
    /// </summary>
    /// <param name="mainData">The <c>maindata</c> folder</param>
    /// <param name="limits">The archive limits</param>
    /// <param name="uncheckedCount">The number of archives that could not be checked</param>
    /// <returns>The copies</returns>
    private static List<GameCatalogCopy> FindCopies(string mainData, RdaLimits limits, out int uncheckedCount)
    {
        var copies = new List<GameCatalogCopy>();
        var paths = new[] { DataSetsPath, AssetsPath, EnglishTextsPath, GermanTextsPath, PropertiesPath, TemplatesPath };
        var wanted = new HashSet<string>(paths, StringComparer.OrdinalIgnoreCase);

        uncheckedCount = 0;

        foreach (var relative in RdaInstallationSurvey.FindArchives(mainData))
        {
            if (string.Equals(relative, ConfigArchiveName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var listed = ListWanted(Path.Combine(mainData, relative), limits, wanted);

            if (listed is null)
            {
                uncheckedCount++;

                continue;
            }

            copies.AddRange(paths.Where(listed.Contains).Select(path => new GameCatalogCopy(relative, path)));
        }

        return copies;
    }

    /// <summary>
    /// Lists which of the wanted paths an archive holds.
    /// </summary>
    /// <param name="archivePath">The path of the archive</param>
    /// <param name="limits">The archive limits</param>
    /// <param name="wanted">The wanted paths, ignoring case</param>
    /// <returns>The wanted paths as the constants spell them, or <c>null</c> when the archive could not be opened</returns>
    private static HashSet<string>? ListWanted(string archivePath, RdaLimits limits, HashSet<string> wanted)
    {
        try
        {
            var opened = RdaArchive.Open(archivePath, limits);

            if (opened.Archive is null)
            {
                return null;
            }

            using var archive = opened.Archive;

            return wanted.Where(path => archive.Entries.Any(entry => string.Equals(entry.Path, path, StringComparison.OrdinalIgnoreCase))).ToHashSet();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return null;
        }
    }

    /// <summary>
    /// Creates the result of a failure with the fixed message of its kind.
    /// </summary>
    /// <param name="kind">The kind</param>
    /// <returns>The result</returns>
    private static GameCatalogResult CreateError(GameCatalogErrorKind kind)
    {
        var message = kind switch
                      {
                          GameCatalogErrorKind.ArchiveUnreadable => "The configuration archive could not be opened.",
                          GameCatalogErrorKind.FileMissing => "A required file is missing from the archive.",
                          GameCatalogErrorKind.FileUnreadable => "A required file could not be read.",
                          _ => "A required file does not have the expected structure."
                      };

        return GameCatalogResult.FromError(new GameCatalogError(kind, message));
    }

    #endregion // Methods
}