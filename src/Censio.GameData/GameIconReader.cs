using System.Globalization;

namespace Censio.GameData;

/// <summary>
/// Reads the icons of the catalog entries from the game's UI archives.
/// </summary>
public static class GameIconReader
{
    #region Constants

    /// <summary>
    /// The longest side in pixels an icon may have.
    /// </summary>
    public const int MaxIconSide = 2048;

    /// <summary>
    /// The most bytes an icon's archive entry may take.
    /// </summary>
    public const int MaxIconEntryBytes = 8388608;

    /// <summary>
    /// The longest icon entry path in characters.
    /// </summary>
    public const int MaxIconPathLength = 1024;

    /// <summary>
    /// The most bytes of PNG data the kept icons may take together.
    /// </summary>
    public const int MaxIconSetBytes = 268435456;

    /// <summary>
    /// The name of the base UI archive.
    /// </summary>
    public const string UiArchiveName = "ui.rda";

    /// <summary>
    /// The folder of the game's archives below the installation folder.
    /// </summary>
    private const string MainDataFolder = "maindata";

    /// <summary>
    /// The start of a patch archive's file name.
    /// </summary>
    private const string PatchPrefix = "zz_patchfiles_";

    /// <summary>
    /// The extension of an archive.
    /// </summary>
    private const string ArchiveExtension = ".rda";

    /// <summary>
    /// The start of the icon file names the assets state.
    /// </summary>
    private const string FhdPrefix = "data/ui/fhd/";

    /// <summary>
    /// The start of the icon entry paths.
    /// </summary>
    private const string FourKPrefix = "data/ui/4k/";

    /// <summary>
    /// The end of the icon file names the assets state.
    /// </summary>
    private const string PngSuffix = ".png";

    /// <summary>
    /// The end of the icon entry paths.
    /// </summary>
    private const string DdsSuffix = "_0.dds";

    #endregion // Constants

    #region Methods

    /// <summary>
    /// Loads the icons of the catalog's entries.
    /// </summary>
    /// <param name="installationFolder">The game's installation folder</param>
    /// <param name="catalog">The catalog whose entries name the icons</param>
    /// <returns>The icons or the reason they could not be loaded</returns>
    public static GameIconResult Load(string installationFolder, GameCatalog catalog)
    {
        return Load(installationFolder, catalog, MaxIconSetBytes);
    }

    /// <summary>
    /// Loads the icons of the catalog's entries; the icon set fails with <see cref="GameIconErrorKind.IconSetTooLarge" /> when the kept PNG bytes go above the given limit.
    /// </summary>
    /// <param name="installationFolder">The game's installation folder</param>
    /// <param name="catalog">The catalog whose entries name the icons</param>
    /// <param name="maxIconSetBytes">The most bytes of PNG data the kept icons may take together</param>
    /// <returns>The icons or the reason they could not be loaded</returns>
    public static GameIconResult Load(string installationFolder, GameCatalog catalog, long maxIconSetBytes)
    {
        ArgumentNullException.ThrowIfNull(installationFolder);
        ArgumentNullException.ThrowIfNull(catalog);

        try
        {
            return LoadCore(installationFolder, catalog, maxIconSetBytes);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return Fail(GameIconErrorKind.ArchiveUnreadable, MainDataFolder);
        }
    }

    /// <summary>
    /// Derives the archive entry path of an icon file name.
    /// </summary>
    /// <param name="iconFilename">The icon file name an asset states</param>
    /// <returns>The entry path, or <c>null</c> when the name yields none</returns>
    public static string? EntryPathOf(string? iconFilename)
    {
        var name = iconFilename?.Trim();

        if (string.IsNullOrEmpty(name)
            || name.Length > MaxIconPathLength
            || name.Length <= FhdPrefix.Length + PngSuffix.Length
            || name.StartsWith(FhdPrefix, StringComparison.OrdinalIgnoreCase) == false
            || name.EndsWith(PngSuffix, StringComparison.OrdinalIgnoreCase) == false)
        {
            return null;
        }

        var rest = name[FhdPrefix.Length..^PngSuffix.Length];

        return $"{FourKPrefix}{rest}{DdsSuffix}";
    }

    /// <summary>
    /// Reads the patch number of a UI archive file name.
    /// </summary>
    /// <param name="fileName">The archive file name</param>
    /// <returns>The patch number, or <c>null</c> when the name is not a patch archive</returns>
    public static uint? PatchNumberOf(string fileName)
    {
        if (fileName.StartsWith(PatchPrefix, StringComparison.OrdinalIgnoreCase) == false || fileName.EndsWith(ArchiveExtension, StringComparison.OrdinalIgnoreCase) == false)
        {
            return null;
        }

        var digits = fileName.AsSpan(PatchPrefix.Length);

        digits = digits[..^ArchiveExtension.Length];

        if (digits.IsEmpty || digits.ContainsAnyExceptInRange('0', '9'))
        {
            return null;
        }

        return uint.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : null;
    }

    /// <summary>
    /// Runs the icon step.
    /// </summary>
    /// <param name="installationFolder">The game's installation folder</param>
    /// <param name="catalog">The catalog</param>
    /// <param name="maxIconSetBytes">The bound of the kept PNG bytes</param>
    /// <returns>The result</returns>
    private static GameIconResult LoadCore(string installationFolder, GameCatalog catalog, long maxIconSetBytes)
    {
        var mainData = Path.Combine(installationFolder, MainDataFolder);
        var archives = RankArchives(RdaInstallationSurvey.FindArchives(mainData), out var failure);

        if (failure is not null)
        {
            return GameIconResult.FromError(failure);
        }

        var problems = new Dictionary<int, GameIconProblem>();
        var sources = CollectWanted(catalog, problems);
        var limits = new RdaLimits(RdaLimits.Default.MaxBlocks, RdaLimits.Default.MaxEntries, RdaLimits.Default.MaxDirectoryBytes, MaxIconEntryBytes);

        for (var index = 0; index < archives.Count; index++)
        {
            if (ListMatches(Path.Combine(mainData, archives[index]), index, limits, sources) == false)
            {
                return Fail(GameIconErrorKind.ArchiveUnreadable, archives[index]);
            }
        }

        var icons = new Dictionary<int, GameIcon>();

        for (var index = 0; index < archives.Count; index++)
        {
            var error = ReadWinners(Path.Combine(mainData, archives[index]), index, limits, sources, maxIconSetBytes, icons, problems);

            if (error is not null)
            {
                return Fail(error.Value, archives[index]);
            }
        }

        MarkMissing(sources, problems);

        return GameIconResult.FromIcons(CreateSet(catalog, icons, problems));
    }

    /// <summary>
    /// Finds the icon archives and orders them by rank: the base archive, then the patches by number.
    /// </summary>
    /// <param name="listing">The relative paths of all archives</param>
    /// <param name="failure">The error, or <c>null</c> when the archives are usable</param>
    /// <returns>The relative paths of the icon archives, lowest rank first</returns>
    private static List<string> RankArchives(List<string> listing, out GameIconError? failure)
    {
        var ranked = new List<(long Rank, string Path)>();
        var numbers = new HashSet<uint>();

        failure = null;

        foreach (var relative in listing)
        {
            if (string.Equals(relative, UiArchiveName, StringComparison.OrdinalIgnoreCase))
            {
                ranked.Add((0, relative));
            }
            else if (PatchNumberOf(Path.GetFileName(relative)) is { } number)
            {
                if (numbers.Add(number) == false)
                {
                    failure = new GameIconError(GameIconErrorKind.DuplicateArchiveNumber, relative);

                    return [];
                }

                ranked.Add((number + 1L, relative));
            }
        }

        if (ranked.Any(item => item.Rank == 0) == false)
        {
            failure = new GameIconError(GameIconErrorKind.ArchiveMissing, UiArchiveName);
        }

        return ranked.OrderBy(item => item.Rank).Select(item => item.Path).ToList();
    }

    /// <summary>
    /// Collects the entry paths the good and region entries want and notes the entries that name none.
    /// </summary>
    /// <param name="catalog">The catalog</param>
    /// <param name="problems">Receives the problems of the entries</param>
    /// <returns>The wanted entries by path, ignoring case</returns>
    private static Dictionary<string, GameIconSource> CollectWanted(GameCatalog catalog, Dictionary<int, GameIconProblem> problems)
    {
        var sources = new Dictionary<string, GameIconSource>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in catalog.Entries.Where(entry => entry.Kind is GameCatalogKind.Good or GameCatalogKind.Region && ReferenceEquals(catalog.Find(entry.AssetGuid), entry)))
        {
            var path = EntryPathOf(entry.IconFilename);

            if (path is null)
            {
                problems.TryAdd(entry.AssetGuid, string.IsNullOrWhiteSpace(entry.IconFilename) ? GameIconProblem.NotNamed : GameIconProblem.Missing);
            }
            else if (sources.TryGetValue(path, out var source) == false)
            {
                sources.Add(path,
                            new GameIconSource
                            {
                                Guids = {
                                            entry.AssetGuid
                                        }
                            });
            }
            else
            {
                source.Guids.Add(entry.AssetGuid);
            }
        }

        return sources;
    }

    /// <summary>
    /// Records which wanted paths an archive holds; a later archive replaces the matches of an earlier one.
    /// </summary>
    /// <param name="path">The archive file</param>
    /// <param name="index">The rank index of the archive</param>
    /// <param name="limits">The limits to open the archive within</param>
    /// <param name="sources">The wanted entries by path</param>
    /// <returns><c>false</c> when the archive could not be opened</returns>
    private static bool ListMatches(string path, int index, RdaLimits limits, Dictionary<string, GameIconSource> sources)
    {
        var opened = RdaArchive.Open(path, limits);

        if (opened.Archive is null)
        {
            return false;
        }

        using var archive = opened.Archive;

        var found = new Dictionary<GameIconSource, List<string>>();

        foreach (var entryPath in archive.Entries.Select(entry => entry.Path))
        {
            if (sources.TryGetValue(entryPath, out var source))
            {
                if (found.TryGetValue(source, out var list) == false)
                {
                    list = [];
                    found.Add(source, list);
                }

                list.Add(entryPath);
            }
        }

        foreach (var (source, list) in found)
        {
            source.ArchiveIndex = index;
            source.Matches.Clear();
            source.Matches.AddRange(list);
        }

        return true;
    }

    /// <summary>
    /// Reads the icons whose winning archive is the given one.
    /// </summary>
    /// <param name="path">The archive file</param>
    /// <param name="index">The rank index of the archive</param>
    /// <param name="limits">The limits to open the archive within</param>
    /// <param name="sources">The wanted entries by path</param>
    /// <param name="maxIconSetBytes">The bound of the kept PNG bytes</param>
    /// <param name="icons">Receives the icons by asset GUID</param>
    /// <param name="problems">Receives the problems by asset GUID</param>
    /// <returns>The kind of the error that ends the step, or <c>null</c></returns>
    private static GameIconErrorKind? ReadWinners(string path, int index, RdaLimits limits, Dictionary<string, GameIconSource> sources, long maxIconSetBytes, Dictionary<int, GameIcon> icons, Dictionary<int, GameIconProblem> problems)
    {
        var wanted = sources.Values.Where(source => source.ArchiveIndex == index).ToList();

        if (wanted.Count == 0)
        {
            return null;
        }

        var opened = RdaArchive.Open(path, limits);

        if (opened.Archive is null)
        {
            return GameIconErrorKind.ArchiveUnreadable;
        }

        using var archive = opened.Archive;

        long total = icons.Values.Distinct().Sum(icon => (long)icon.Png.Length);

        foreach (var source in wanted)
        {
            var icon = ReadIcon(archive, source);

            if (icon is null)
            {
                source.Guids.ForEach(guid => problems.TryAdd(guid, GameIconProblem.Unreadable));

                continue;
            }

            total += icon.Png.Length;

            if (total > maxIconSetBytes)
            {
                return GameIconErrorKind.IconSetTooLarge;
            }

            source.Guids.ForEach(guid => icons.TryAdd(guid, icon));
        }

        return null;
    }

    /// <summary>
    /// Reads and converts the icon of a wanted path.
    /// </summary>
    /// <param name="archive">The winning archive</param>
    /// <param name="source">The wanted entry</param>
    /// <returns>The icon, or <c>null</c> when it is not readable</returns>
    private static GameIcon? ReadIcon(RdaArchive archive, GameIconSource source)
    {
        if (source.Matches.Count != 1)
        {
            return null;
        }

        var read = archive.Read(source.Matches[0]);

        if (read.Error is not null)
        {
            return null;
        }

        var image = DdsReader.Read(read.Content.Span, MaxIconSide);

        return image is null ? null : new GameIcon(image.Width, image.Height, PngWriter.Write(image.Width, image.Height, image.Rgba.Span));
    }

    /// <summary>
    /// Marks the entries whose path no archive holds as missing.
    /// </summary>
    /// <param name="sources">The wanted entries by path</param>
    /// <param name="problems">Receives the problems by asset GUID</param>
    private static void MarkMissing(Dictionary<string, GameIconSource> sources, Dictionary<int, GameIconProblem> problems)
    {
        foreach (var source in sources.Values.Where(source => source.ArchiveIndex < 0))
        {
            source.Guids.ForEach(guid => problems.TryAdd(guid, GameIconProblem.Missing));
        }
    }

    /// <summary>
    /// Creates the icon set.
    /// </summary>
    /// <param name="catalog">The catalog</param>
    /// <param name="icons">The icons by asset GUID</param>
    /// <param name="problems">The problems by asset GUID</param>
    /// <returns>The set</returns>
    private static GameIconSet CreateSet(GameCatalog catalog, Dictionary<int, GameIcon> icons, Dictionary<int, GameIconProblem> problems)
    {
        var goods = catalog.Entries.Count(entry => entry.Kind == GameCatalogKind.Good && icons.ContainsKey(entry.AssetGuid));
        var regions = catalog.Entries.Count(entry => entry.Kind == GameCatalogKind.Region && icons.ContainsKey(entry.AssetGuid));

        return new GameIconSet(icons, problems, goods, regions);
    }

    /// <summary>
    /// Creates the failed result of an error.
    /// </summary>
    /// <param name="kind">The kind</param>
    /// <param name="archive">The relative path of the archive</param>
    /// <returns>The result</returns>
    private static GameIconResult Fail(GameIconErrorKind kind, string archive)
    {
        return GameIconResult.FromError(new GameIconError(kind, archive));
    }

    #endregion // Methods
}