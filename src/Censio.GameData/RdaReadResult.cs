namespace Censio.GameData;

/// <summary>
/// Holds the content of an entry or the reason it could not be read.
/// </summary>
public sealed class RdaReadResult
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="RdaReadResult" /> class.
    /// </summary>
    /// <param name="content">The content</param>
    /// <param name="error">The error</param>
    private RdaReadResult(ReadOnlyMemory<byte> content, RdaError? error)
    {
        Content = content;
        Error = error;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the decompressed content; empty on an error.
    /// </summary>
    public ReadOnlyMemory<byte> Content { get; }

    /// <summary>
    /// Gets the error, or <c>null</c> when the entry was read.
    /// </summary>
    public RdaError? Error { get; }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Creates the result of a read entry.
    /// </summary>
    /// <param name="content">The content</param>
    /// <returns>The result</returns>
    public static RdaReadResult FromContent(ReadOnlyMemory<byte> content)
    {
        return new RdaReadResult(content, null);
    }

    /// <summary>
    /// Creates the result of a failed read.
    /// </summary>
    /// <param name="error">The error</param>
    /// <returns>The result</returns>
    public static RdaReadResult FromError(RdaError error)
    {
        return new RdaReadResult(ReadOnlyMemory<byte>.Empty, error);
    }

    #endregion // Methods
}