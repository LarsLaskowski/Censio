namespace Censio.GameData;

/// <summary>
/// Describes why the catalog could not be built.
/// </summary>
public sealed class GameCatalogError
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="GameCatalogError" /> class.
    /// </summary>
    /// <param name="kind">The kind</param>
    /// <param name="message">The message</param>
    public GameCatalogError(GameCatalogErrorKind kind, string message)
    {
        Kind = kind;
        Message = message;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the kind.
    /// </summary>
    public GameCatalogErrorKind Kind { get; }

    /// <summary>
    /// Gets the message.
    /// </summary>
    public string Message { get; }

    #endregion // Properties
}