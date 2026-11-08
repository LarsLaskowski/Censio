namespace Censio.GameData;

/// <summary>
/// Collects what the survey of an installation finds while it walks the archives one by one.
/// </summary>
internal sealed class RdaSurveyState
{
    #region Properties

    /// <summary>
    /// Gets the matching XML entries: path to the size and the archives that hold it.
    /// </summary>
    public Dictionary<string, (long Size, List<string> Archives)> Matching { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the folders of icon entries: folder to one archive name per entry.
    /// </summary>
    public Dictionary<string, List<string>> IconFolders { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the entries of the language archives that are not media.
    /// </summary>
    public List<string> LanguageEntries { get; } = [];

    /// <summary>
    /// Gets or sets the smallest asset XML of <c>config.rda</c>.
    /// </summary>
    public (string Archive, string Path, long Size)? ConfigAsset { get; set; }

    /// <summary>
    /// Gets or sets the smallest XML of <c>config.rda</c>.
    /// </summary>
    public (string Archive, string Path, long Size)? ConfigXml { get; set; }

    /// <summary>
    /// Gets or sets the smallest icon of <c>ui.rda</c>.
    /// </summary>
    public (string Archive, string Path, long Size)? UiIcon { get; set; }

    /// <summary>
    /// Gets or sets the smallest icon of any archive.
    /// </summary>
    public (string Archive, string Path, long Size)? AnyIcon { get; set; }

    #endregion // Properties
}