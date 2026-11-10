namespace Censio.GameData;

/// <summary>
/// Holds the loaded icons or the reason they could not be loaded.
/// </summary>
public sealed class GameIconResult
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="GameIconResult" /> class.
    /// </summary>
    /// <param name="icons">The icons</param>
    /// <param name="error">The error</param>
    private GameIconResult(GameIconSet? icons, GameIconError? error)
    {
        Icons = icons;
        Error = error;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the icons, or <c>null</c> on an error.
    /// </summary>
    public GameIconSet? Icons { get; }

    /// <summary>
    /// Gets the error, or <c>null</c> when the icons were loaded.
    /// </summary>
    public GameIconError? Error { get; }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Creates the result of loaded icons.
    /// </summary>
    /// <param name="icons">The icons</param>
    /// <returns>The result</returns>
    public static GameIconResult FromIcons(GameIconSet icons)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Creates the result of a failed load.
    /// </summary>
    /// <param name="error">The error</param>
    /// <returns>The result</returns>
    public static GameIconResult FromError(GameIconError error)
    {
        throw new NotImplementedException();
    }

    #endregion // Methods
}