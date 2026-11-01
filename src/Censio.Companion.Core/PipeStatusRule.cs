using Censio.Pipe;

namespace Censio.Companion.Core;

/// <summary>
/// Maps a read result of the statistics pipe to the connection status it causes.
/// </summary>
public static class PipeStatusRule
{
    #region Methods

    /// <summary>
    /// Gets the status a read result causes.
    /// </summary>
    /// <param name="read">The read result</param>
    /// <returns>The status, or <c>null</c> if the result does not change the status</returns>
    public static PipeConnectionStatus? ForRead(PipeReadResult read)
    {
        if (read.Kind == PipeReadResultKind.Message)
        {
            return new PipeConnectionStatus
                   {
                       State = PipeConnectionState.Receiving
                   };
        }

        if (read.Kind == PipeReadResultKind.EndOfStream)
        {
            return new PipeConnectionStatus
                   {
                       State = PipeConnectionState.WaitingForGame
                   };
        }

        var error = read.Error!;

        if (error.Kind == PipeErrorKind.ProtocolMismatch)
        {
            return new PipeConnectionStatus
                   {
                       State = PipeConnectionState.ProtocolMismatch,
                       ReceivedVersion = error.ReceivedVersion
                   };
        }

        if (error.Kind is PipeErrorKind.TruncatedFrame or PipeErrorKind.OversizedFrame or PipeErrorKind.ReadFailed)
        {
            return new PipeConnectionStatus
                   {
                       State = PipeConnectionState.Error,
                       ErrorKind = error.Kind
                   };
        }

        return null;
    }

    #endregion // Methods
}