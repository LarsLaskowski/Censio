namespace Censio.GameData;

/// <summary>
/// Describes why the icons could not be loaded; it never carries archive content.
/// </summary>
public sealed class GameIconError
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="GameIconError" /> class.
    /// </summary>
    /// <param name="kind">The reason of the error</param>
    /// <param name="archive">The file name of the archive concerned</param>
    public GameIconError(GameIconErrorKind kind, string archive)
    {
        Kind = kind;
        Archive = archive;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the reason of the error.
    /// </summary>
    public GameIconErrorKind Kind { get; }

    /// <summary>
    /// Gets the file name of the archive concerned.
    /// </summary>
    public string Archive { get; }

    #endregion // Properties
}