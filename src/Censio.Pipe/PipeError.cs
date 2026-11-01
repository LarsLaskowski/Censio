namespace Censio.Pipe;

/// <summary>
/// Describes why a frame or a message could not be read; it never carries payload text.
/// </summary>
public sealed class PipeError
{
    #region Properties

    /// <summary>
    /// Gets the reason of the error.
    /// </summary>
    public required PipeErrorKind Kind { get; init; }

    /// <summary>
    /// Gets the announced frame length, when known.
    /// </summary>
    public int? Length { get; init; }

    /// <summary>
    /// Gets the type byte of the frame, when known.
    /// </summary>
    public byte? TypeValue { get; init; }

    /// <summary>
    /// Gets the position of the field that did not fit, when known.
    /// </summary>
    public int? Offset { get; init; }

    /// <summary>
    /// Gets the protocol version the pipe announced, when known.
    /// </summary>
    public int? ReceivedVersion { get; init; }

    /// <summary>
    /// Gets the protocol version this reader expects, when relevant.
    /// </summary>
    public int? ExpectedVersion { get; init; }

    #endregion // Properties

    #region Object

    /// <inheritdoc />
    public override string ToString()
    {
        throw new NotImplementedException();
    }

    #endregion // Object
}