namespace Censio.GameData;

/// <summary>
/// Holds the size and modification time of one archive file of the game installation.
/// </summary>
public sealed record GameArchiveState
{
    #region Properties

    /// <summary>
    /// Gets the path of the archive relative to the <c>maindata</c> folder.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Gets the length of the archive file in bytes.
    /// </summary>
    public required long Length { get; init; }

    /// <summary>
    /// Gets the last write time of the archive file in UTC.
    /// </summary>
    public required DateTime LastWriteTimeUtc { get; init; }

    #endregion // Properties
}