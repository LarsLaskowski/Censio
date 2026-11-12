using Censio.Contracts;
using Censio.Pipe;

namespace Censio.Companion.Core;

/// <summary>
/// Converts the Companion's pipe data into the messages of the contract.
/// </summary>
public static class ContractMessageConverter
{
    #region Methods

    /// <summary>
    /// Converts a closed pipe tick into a contract tick with a new identity.
    /// </summary>
    /// <param name="tick">The pipe tick</param>
    /// <param name="profileId">The profile identity</param>
    /// <returns>The contract tick</returns>
#pragma warning disable RH2003 // Skeleton: removed with the implementation
    public static ContractTick ToTick(PipeTick tick, Guid profileId)
    {
        throw new NotImplementedException();
    }
#pragma warning restore RH2003 // Skeleton: removed with the implementation

    /// <summary>
    /// Creates a session start message with a new identity.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <param name="receiveTime">The read stamp of the session message</param>
    /// <returns>The session start message</returns>
#pragma warning disable RH2003 // Skeleton: removed with the implementation
    public static ContractSessionStart ToSessionStart(Guid profileId, DateTimeOffset receiveTime)
    {
        throw new NotImplementedException();
    }
#pragma warning restore RH2003 // Skeleton: removed with the implementation

    /// <summary>
    /// Creates a session end message with a new identity.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <param name="receiveTime">The read stamp of the session message</param>
    /// <returns>The session end message</returns>
#pragma warning disable RH2003 // Skeleton: removed with the implementation
    public static ContractSessionEnd ToSessionEnd(Guid profileId, DateTimeOffset receiveTime)
    {
        throw new NotImplementedException();
    }
#pragma warning restore RH2003 // Skeleton: removed with the implementation

    /// <summary>
    /// Converts the pipe connection state into the contract's pipe state.
    /// </summary>
    /// <param name="state">The pipe connection state</param>
    /// <returns>The status message</returns>
#pragma warning disable RH2003 // Skeleton: removed with the implementation
    public static ContractStatus ToStatus(PipeConnectionState state)
    {
        throw new NotImplementedException();
    }
#pragma warning restore RH2003 // Skeleton: removed with the implementation

    #endregion // Methods
}