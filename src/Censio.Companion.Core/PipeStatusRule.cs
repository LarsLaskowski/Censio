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
        throw new NotImplementedException();
    }

    #endregion // Methods
}