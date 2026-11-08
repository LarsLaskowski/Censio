namespace Censio.GameData;

/// <summary>
/// Holds the opened archive or the reason it could not be opened.
/// </summary>
public sealed class RdaOpenResult
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="RdaOpenResult" /> class.
    /// </summary>
    /// <param name="archive">The opened archive</param>
    /// <param name="error">The error</param>
    private RdaOpenResult(RdaArchive? archive, RdaError? error)
    {
        Archive = archive;
        Error = error;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the opened archive, or <c>null</c> on an error.
    /// </summary>
    public RdaArchive? Archive { get; }

    /// <summary>
    /// Gets the error, or <c>null</c> when the archive opened.
    /// </summary>
    public RdaError? Error { get; }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Creates the result of an opened archive.
    /// </summary>
    /// <param name="archive">The opened archive</param>
    /// <returns>The result</returns>
    public static RdaOpenResult FromArchive(RdaArchive archive)
    {
        return new RdaOpenResult(archive, null);
    }

    /// <summary>
    /// Creates the result of a failed open.
    /// </summary>
    /// <param name="error">The error</param>
    /// <returns>The result</returns>
    public static RdaOpenResult FromError(RdaError error)
    {
        return new RdaOpenResult(null, error);
    }

    #endregion // Methods
}