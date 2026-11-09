namespace Censio.GameData;

/// <summary>
/// Holds the stream of a catalog file or the reason it could not be opened.
/// </summary>
public sealed class GameCatalogSource
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="GameCatalogSource" /> class.
    /// </summary>
    /// <param name="stream">The stream</param>
    /// <param name="errorKind">The error kind</param>
    private GameCatalogSource(Stream? stream, GameCatalogErrorKind? errorKind)
    {
        Stream = stream;
        ErrorKind = errorKind;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the stream, or <c>null</c> on an error.
    /// </summary>
    public Stream? Stream { get; }

    /// <summary>
    /// Gets the error kind, or <c>null</c> when the file opened.
    /// </summary>
    public GameCatalogErrorKind? ErrorKind { get; }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Creates the source of an opened file.
    /// </summary>
    /// <param name="stream">The stream</param>
    /// <returns>The source</returns>
    public static GameCatalogSource FromStream(Stream stream)
    {
        return new GameCatalogSource(stream, null);
    }

    /// <summary>
    /// Creates the source of a file that could not be opened.
    /// </summary>
    /// <param name="kind">The error kind</param>
    /// <returns>The source</returns>
    public static GameCatalogSource FromError(GameCatalogErrorKind kind)
    {
        return new GameCatalogSource(null, kind);
    }

    #endregion // Methods
}