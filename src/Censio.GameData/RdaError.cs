namespace Censio.GameData;

/// <summary>
/// Describes why an archive or an entry could not be read; it never carries archive content.
/// </summary>
public sealed class RdaError
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="RdaError" /> class.
    /// </summary>
    /// <param name="kind">The reason of the error</param>
    /// <param name="message">The fixed English message</param>
    public RdaError(RdaErrorKind kind, string message)
    {
        Kind = kind;
        Message = message;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the reason of the error.
    /// </summary>
    public RdaErrorKind Kind { get; }

    /// <summary>
    /// Gets the fixed English message.
    /// </summary>
    public string Message { get; }

    #endregion // Properties
}