namespace Censio.Companion.Core;

/// <summary>
/// The status of the game installation search.
/// </summary>
public sealed record GameInstallationStatus
{
    #region Properties

    /// <summary>
    /// Gets the status while the search runs.
    /// </summary>
    public static GameInstallationStatus Searching { get; } = new()
                                                              {
                                                                  Kind = GameInstallationStatusKind.Searching
                                                              };

    /// <summary>
    /// Gets the outcome kind.
    /// </summary>
    public required GameInstallationStatusKind Kind { get; init; }

    /// <summary>
    /// Gets where the installation was found, if it was.
    /// </summary>
    public GameInstallationSource? Source { get; init; }

    /// <summary>
    /// Gets the folder of the installation, if it was found or named.
    /// </summary>
    public string? Folder { get; init; }

    /// <summary>
    /// Gets the build of the installation, if it is known.
    /// </summary>
    public string? Build { get; init; }

    #endregion // Properties
}