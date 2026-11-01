namespace Censio.Pipe;

/// <summary>
/// Decodes one frame (type byte and payload) into a typed pipe message.
/// </summary>
public static class PipeMessageParser
{
    #region Methods

    /// <summary>
    /// Decodes a frame without applying the version gate.
    /// </summary>
    /// <param name="frame">The frame bytes (type byte and payload)</param>
    /// <returns>The decoded message, or the error that describes why the frame is rejected</returns>
    public static PipeReadResult Parse(ReadOnlySpan<byte> frame)
    {
        throw new NotImplementedException();
    }

    #endregion // Methods
}