namespace Censio.GameData;

/// <summary>
/// Lists the archive files below the <c>maindata</c> folder of a game installation with their size and time.
/// </summary>
public static class GameArchiveStateReader
{
    #region Methods

    /// <summary>
    /// Reads the state of all archive files below the <c>maindata</c> folder.
    /// </summary>
    /// <param name="installationFolder">The installation folder</param>
    /// <returns>The archives in a stable order</returns>
    public static IReadOnlyList<GameArchiveState> Read(string installationFolder)
    {
        throw new NotImplementedException();
    }

    #endregion // Methods
}