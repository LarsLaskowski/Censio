using System.Text;

namespace Censio.GameData;

/// <summary>
/// Surveys the archives of an Anno 117 installation: what they hold and whether an asset file and an icon can be read.
/// </summary>
public static class RdaInstallationSurvey
{
    #region Constants

    /// <summary>
    /// The words of which one in the path makes an XML entry worth listing.
    /// </summary>
    private static readonly string[] _matchingWords = ["asset", "text", "guid", "region", "province", "session", "dataset"];

    /// <summary>
    /// The extensions of audio and video entries, which the language archives list leaves out.
    /// </summary>
    private static readonly string[] _mediaExtensions = [".wem", ".bnk", ".wav", ".ogg", ".bik", ".webm", ".mp4", ".usm"];

    /// <summary>
    /// The language archives whose entries are listed.
    /// </summary>
    private static readonly string[] _languageArchives = ["de_de0.rda", "en_us0.rda"];

    #endregion // Constants

    #region Methods

    /// <summary>
    /// Surveys the archives below <c>maindata</c> of an installation and reads one asset file and one icon.
    /// </summary>
    /// <param name="installationFolder">The installation folder</param>
    /// <param name="limits">The limits to read the archives within</param>
    /// <returns>The report; it holds no entry content</returns>
    public static RdaSurveyReport Run(string installationFolder, RdaLimits limits)
    {
        ArgumentNullException.ThrowIfNull(installationFolder);
        ArgumentNullException.ThrowIfNull(limits);

        var mainData = Path.Combine(installationFolder, "maindata");
        var archives = FindArchives(mainData);

        if (archives.Count == 0)
        {
            return new RdaSurveyReport(["Archives: none under maindata"], false, false);
        }

        var lines = new List<string>
                    {
                        $"Archives: {archives.Count}"
                    };
        var state = new RdaSurveyState();

        foreach (var relative in archives)
        {
            SurveyArchive(mainData, relative, limits, state, lines);
        }

        AddSections(state, lines);

        var (assetLine, assetRead) = ReadSample("Asset read", state.ConfigAsset ?? state.ConfigXml, mainData, limits, IsXml, "XML");
        var (iconLine, iconRead) = ReadSample("Icon read", state.UiIcon ?? state.AnyIcon, mainData, limits, IsDds, "DDS");

        lines.Add(assetLine);
        lines.Add(iconLine);

        return new RdaSurveyReport(lines, assetRead, iconRead);
    }

    /// <summary>
    /// Finds the archives below <c>maindata</c>, links and inaccessible folders left out.
    /// </summary>
    /// <param name="mainData">The <c>maindata</c> folder</param>
    /// <returns>The relative paths, ordinal and ignoring case</returns>
    private static List<string> FindArchives(string mainData)
    {
        if (Directory.Exists(mainData) == false)
        {
            return [];
        }

        var options = new EnumerationOptions
                      {
                          RecurseSubdirectories = true,
                          IgnoreInaccessible = true,
                          AttributesToSkip = FileAttributes.ReparsePoint
                      };

        return Directory.EnumerateFiles(mainData, "*", options)
                        .Where(file => file.EndsWith(".rda", StringComparison.OrdinalIgnoreCase))
                        .Select(file => Path.GetRelativePath(mainData, file))
                        .Order(StringComparer.OrdinalIgnoreCase)
                        .ToList();
    }

    /// <summary>
    /// Surveys one archive and adds its lines.
    /// </summary>
    /// <param name="mainData">The <c>maindata</c> folder</param>
    /// <param name="relative">The relative path of the archive</param>
    /// <param name="limits">The limits to read within</param>
    /// <param name="state">What the survey has found so far</param>
    /// <param name="lines">The lines of the report</param>
    private static void SurveyArchive(string mainData, string relative, RdaLimits limits, RdaSurveyState state, List<string> lines)
    {
        var result = RdaArchive.Open(Path.Combine(mainData, relative), limits);

        if (result.Archive is null)
        {
            lines.Add($"{relative} | error {result.Error?.Kind}: {result.Error?.Message}");

            return;
        }

        using var archive = result.Archive;

        var fileName = Path.GetFileName(relative);
        var duplicates = archive.Entries.GroupBy(entry => entry.Path, StringComparer.Ordinal).Count(group => group.Count() > 1);
        var flags = archive.Entries.GroupBy(entry => entry.Flags).OrderBy(group => group.Key).Select(group => $"{group.Key.ToString().Replace(", ", "+", StringComparison.Ordinal)}={group.Count()}");

        lines.Add($"{relative} | entries {archive.Entries.Count} | flags {JoinOr(flags, "none")} | encrypted blocks {archive.EncryptedBlockCount} | duplicate paths {duplicates}");
        lines.Add("  extensions: " + JoinOr(Counts(archive.Entries.Select(entry => ExtensionOf(entry.Path))), "none"));
        lines.Add("  folders: " + JoinOr(Counts(archive.Entries.Select(entry => FolderOf(entry.Path))), "none"));

        foreach (var entry in archive.Entries)
        {
            Collect(state, relative, fileName, entry);
        }
    }

    /// <summary>
    /// Adds an entry to what the survey collects.
    /// </summary>
    /// <param name="state">What the survey has found so far</param>
    /// <param name="relative">The relative path of the archive</param>
    /// <param name="fileName">The file name of the archive</param>
    /// <param name="entry">The entry</param>
    private static void Collect(RdaSurveyState state, string relative, string fileName, RdaEntry entry)
    {
        var extension = ExtensionOf(entry.Path);
        var isXml = extension == ".xml";
        var isIcon = extension == ".dds" && Contains(entry.Path, "icon");

        if (isXml && _matchingWords.Any(word => Contains(entry.Path, word)))
        {
            AddMatching(state, relative, entry);
        }

        if (isIcon)
        {
            AddIcon(state, relative, fileName, entry);
        }

        if (_languageArchives.Contains(fileName, StringComparer.OrdinalIgnoreCase) && _mediaExtensions.Contains(extension) == false)
        {
            state.LanguageEntries.Add($"{relative} | {entry.Path} | {entry.Size}");
        }

        if (isXml && entry.Size >= 0 && string.Equals(fileName, "config.rda", StringComparison.OrdinalIgnoreCase))
        {
            state.ConfigXml = Smaller(state.ConfigXml, (relative, entry.Path, entry.Size));

            if (Contains(entry.Path, "asset"))
            {
                state.ConfigAsset = Smaller(state.ConfigAsset, (relative, entry.Path, entry.Size));
            }
        }
    }

    /// <summary>
    /// Adds a matching XML entry.
    /// </summary>
    /// <param name="state">What the survey has found so far</param>
    /// <param name="relative">The relative path of the archive</param>
    /// <param name="entry">The entry</param>
    private static void AddMatching(RdaSurveyState state, string relative, RdaEntry entry)
    {
        if (state.Matching.TryGetValue(entry.Path, out var known) == false)
        {
            known = (entry.Size, []);
            state.Matching[entry.Path] = known;
        }

        if (known.Archives.Contains(relative) == false)
        {
            known.Archives.Add(relative);
        }
    }

    /// <summary>
    /// Adds an icon entry.
    /// </summary>
    /// <param name="state">What the survey has found so far</param>
    /// <param name="relative">The relative path of the archive</param>
    /// <param name="fileName">The file name of the archive</param>
    /// <param name="entry">The entry</param>
    private static void AddIcon(RdaSurveyState state, string relative, string fileName, RdaEntry entry)
    {
        var folder = FolderOf(entry.Path, int.MaxValue);

        if (state.IconFolders.TryGetValue(folder, out var archives) == false)
        {
            archives = [];
            state.IconFolders[folder] = archives;
        }

        archives.Add(relative);

        if (entry.Size < 0)
        {
            return;
        }

        state.AnyIcon = Smaller(state.AnyIcon, (relative, entry.Path, entry.Size));

        if (string.Equals(fileName, "ui.rda", StringComparison.OrdinalIgnoreCase))
        {
            state.UiIcon = Smaller(state.UiIcon, (relative, entry.Path, entry.Size));
        }
    }

    /// <summary>
    /// Chooses the smaller of the current and a new candidate; a tie goes to the earlier archive, then the lower path.
    /// </summary>
    /// <param name="current">The current candidate</param>
    /// <param name="candidate">The new candidate</param>
    /// <returns>The chosen one</returns>
    private static (string Archive, string Path, long Size)? Smaller((string Archive, string Path, long Size)? current, (string Archive, string Path, long Size) candidate)
    {
        if (current is null || candidate.Size < current.Value.Size)
        {
            return candidate;
        }

        var sameArchive = string.Equals(candidate.Archive, current.Value.Archive, StringComparison.Ordinal);

        return candidate.Size == current.Value.Size && sameArchive && string.CompareOrdinal(candidate.Path, current.Value.Path) < 0 ? candidate : current;
    }

    /// <summary>
    /// Adds the sections that list what the walk collected.
    /// </summary>
    /// <param name="state">What the survey has found</param>
    /// <param name="lines">The lines of the report</param>
    private static void AddSections(RdaSurveyState state, List<string> lines)
    {
        lines.Add("Matching entries:");
        lines.AddRange(state.Matching.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"  {pair.Key} | {pair.Value.Size} | {string.Join(", ", pair.Value.Archives)}"));
        lines.Add("Icon folders:");
        lines.AddRange(state.IconFolders.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"  {pair.Key} | {pair.Value.Count} | {string.Join(", ", pair.Value.Distinct())}"));
        lines.Add("Language archive entries:");
        lines.AddRange(state.LanguageEntries.Select(line => "  " + line));
    }

    /// <summary>
    /// Reads the sample entry and reports its marker.
    /// </summary>
    /// <param name="label">The label of the line</param>
    /// <param name="candidate">The chosen entry, if any</param>
    /// <param name="mainData">The <c>maindata</c> folder</param>
    /// <param name="limits">The limits to read within</param>
    /// <param name="hasMarker">Tells whether the content has the expected marker</param>
    /// <param name="marker">The name of the marker</param>
    /// <returns>The line and whether the marker was recognized</returns>
    private static (string Line, bool Recognized) ReadSample(string label, (string Archive, string Path, long Size)? candidate, string mainData, RdaLimits limits, Func<byte[], bool> hasMarker, string marker)
    {
        if (candidate is not { } sample)
        {
            return ($"{label}: none found", false);
        }

        var prefix = $"{label}: {sample.Archive} | {sample.Path} | {sample.Size} | ";
        var opened = RdaArchive.Open(Path.Combine(mainData, sample.Archive), limits);

        if (opened.Archive is null)
        {
            return (prefix + $"error {opened.Error?.Kind}: {opened.Error?.Message}", false);
        }

        using var archive = opened.Archive;

        var read = archive.Read(sample.Path);

        if (read.Error is not null)
        {
            return (prefix + $"error {read.Error.Kind}: {read.Error.Message}", false);
        }

        var recognized = hasMarker(read.Content.ToArray());

        return (prefix + (recognized ? marker : "unknown marker"), recognized);
    }

    /// <summary>
    /// Tells whether content is XML: after an optional byte order mark and white space it starts with a bracket.
    /// </summary>
    /// <param name="content">The content</param>
    /// <returns>True for XML</returns>
    private static bool IsXml(byte[] content)
    {
        var step = 1;
        var index = 0;
        var bigEndian = false;

        if (content.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()))
        {
            index = 3;
        }
        else if (content.AsSpan().StartsWith(Encoding.Unicode.GetPreamble()))
        {
            (step, index) = (2, 2);
        }
        else if (content.AsSpan().StartsWith(Encoding.BigEndianUnicode.GetPreamble()))
        {
            (step, index, bigEndian) = (2, 2, true);
        }

        while (index + step <= content.Length)
        {
            var character = ReadCharacter(content, index, step == 2, bigEndian);

            if (char.IsWhiteSpace(character) == false)
            {
                return character == '<';
            }

            index += step;
        }

        return false;
    }

    /// <summary>
    /// Reads one character of content in a byte order mark's encoding.
    /// </summary>
    /// <param name="content">The content</param>
    /// <param name="index">The position</param>
    /// <param name="wide">Whether a character takes two bytes</param>
    /// <param name="bigEndian">Whether the two bytes are big-endian</param>
    /// <returns>The character</returns>
    private static char ReadCharacter(byte[] content, int index, bool wide, bool bigEndian)
    {
        if (wide == false)
        {
            return (char)content[index];
        }

        var (high, low) = bigEndian ? (content[index], content[index + 1]) : (content[index + 1], content[index]);

        return (char)((high << 8) | low);
    }

    /// <summary>
    /// Tells whether content is a DDS texture: it starts with <c>DDS </c>.
    /// </summary>
    /// <param name="content">The content</param>
    /// <returns>True for DDS</returns>
    private static bool IsDds(byte[] content)
    {
        return content.AsSpan().StartsWith("DDS "u8);
    }

    /// <summary>
    /// Counts the values and formats them, the larger count first.
    /// </summary>
    /// <param name="values">The values</param>
    /// <returns>The formatted counts</returns>
    private static IEnumerable<string> Counts(IEnumerable<string> values)
    {
        return values.GroupBy(value => value, StringComparer.Ordinal)
                     .OrderByDescending(group => group.Count())
                     .ThenBy(group => group.Key, StringComparer.Ordinal)
                     .Select(group => $"{group.Key}={group.Count()}");
    }

    /// <summary>
    /// Joins values with a comma, or gives a text when there are none.
    /// </summary>
    /// <param name="values">The values</param>
    /// <param name="empty">The text for no values</param>
    /// <returns>The joined text</returns>
    private static string JoinOr(IEnumerable<string> values, string empty)
    {
        var list = values.ToList();

        return list.Count == 0 ? empty : string.Join(", ", list);
    }

    /// <summary>
    /// Gets the lower-case extension of an entry path.
    /// </summary>
    /// <param name="path">The entry path</param>
    /// <returns>The extension with its dot, or <c>(none)</c></returns>
    private static string ExtensionOf(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();

        return extension.Length == 0 ? "(none)" : extension;
    }

    /// <summary>
    /// Gets the first segments of the folder of an entry path.
    /// </summary>
    /// <param name="path">The entry path</param>
    /// <param name="segments">The most segments to keep</param>
    /// <returns>The folder, or <c>(root)</c></returns>
    private static string FolderOf(string path, int segments = 2)
    {
        var parts = path.Split('/');
        var folder = string.Join('/', parts.Take(parts.Length - 1).Take(segments));

        return folder.Length == 0 ? "(root)" : folder;
    }

    /// <summary>
    /// Tells whether a text contains a word, ignoring case.
    /// </summary>
    /// <param name="text">The text</param>
    /// <param name="word">The word</param>
    /// <returns>True when it does</returns>
    private static bool Contains(string text, string word)
    {
        return text.Contains(word, StringComparison.OrdinalIgnoreCase);
    }

    #endregion // Methods
}