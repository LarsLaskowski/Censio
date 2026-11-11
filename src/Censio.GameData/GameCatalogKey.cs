namespace Censio.GameData;

/// <summary>
/// Identifies the Companion build and the game state a cached catalog was extracted from.
/// </summary>
public sealed class GameCatalogKey
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="GameCatalogKey" /> class.
    /// </summary>
    /// <param name="identity">The identity of the Companion build</param>
    /// <param name="folder">The installation folder</param>
    /// <param name="build">The game build, or <c>null</c> when unknown</param>
    /// <param name="archives">The archives below the <c>maindata</c> folder</param>
    public GameCatalogKey(string identity, string folder, string? build, IReadOnlyList<GameArchiveState> archives)
    {
        Identity = identity;
        Folder = folder;
        Build = build;
        Archives = archives;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the identity of the Companion build.
    /// </summary>
    public string Identity { get; }

    /// <summary>
    /// Gets the installation folder.
    /// </summary>
    public string Folder { get; }

    /// <summary>
    /// Gets the game build, or <c>null</c> when unknown.
    /// </summary>
    public string? Build { get; }

    /// <summary>
    /// Gets the archives below the <c>maindata</c> folder.
    /// </summary>
    public IReadOnlyList<GameArchiveState> Archives { get; }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Checks whether another key describes the same game state, ignoring the Companion identity.
    /// </summary>
    /// <param name="other">The other key</param>
    /// <returns><c>true</c> when folder, build and archives are the same</returns>
    public bool IsSameGame(GameCatalogKey other)
    {
        if (string.Equals(Folder, other.Folder, StringComparison.OrdinalIgnoreCase) == false
            || string.Equals(Build, other.Build, StringComparison.Ordinal) == false
            || Archives.Count != other.Archives.Count)
        {
            return false;
        }

        for (var index = 0; index < Archives.Count; index++)
        {
            var archive = other.Archives[index];

            if (IsSameArchive(Archives[index], archive.Path, archive.Length, archive.LastWriteTimeUtc.Ticks) == false)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Checks whether an archive state has the given path, length and time.
    /// </summary>
    /// <param name="state">The state</param>
    /// <param name="path">The path, compared ignoring case</param>
    /// <param name="length">The length in bytes</param>
    /// <param name="ticks">The ticks of the last write time</param>
    /// <returns><c>true</c> when all three are the same</returns>
    internal static bool IsSameArchive(GameArchiveState state, string path, long length, long ticks)
    {
        return string.Equals(state.Path, path, StringComparison.OrdinalIgnoreCase)
               && state.Length == length
               && state.LastWriteTimeUtc.Ticks == ticks;
    }

    #endregion // Methods
}