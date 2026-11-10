namespace Censio.GameData;

/// <summary>
/// Holds what the icon step knows about one wanted entry path.
/// </summary>
internal sealed class GameIconSource
{
    #region Properties

    /// <summary>
    /// Gets the asset GUIDs of the entries that name the path.
    /// </summary>
    internal List<int> Guids { get; } = [];

    /// <summary>
    /// Gets or sets the index of the highest-ranked archive that holds the path, <c>-1</c> when none does.
    /// </summary>
    internal int ArchiveIndex { get; set; } = -1;

    /// <summary>
    /// Gets the paths of the entries of the winning archive that match.
    /// </summary>
    internal List<string> Matches { get; } = [];

    #endregion // Properties
}