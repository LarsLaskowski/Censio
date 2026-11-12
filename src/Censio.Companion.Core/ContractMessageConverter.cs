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
    public static ContractTick ToTick(PipeTick tick, Guid profileId)
    {
        ArgumentNullException.ThrowIfNull(tick);

        return new ContractTick
               {
                   Id = Guid.NewGuid(),
                   ProfileId = profileId,
                   ReceiveTime = tick.ReceiveTime.ToUniversalTime(),
                   GameTime = tick.GameTime,
                   Islands = [.. tick.Islands.Select(ToIsland)]
               };
    }

    /// <summary>
    /// Creates a session start message with a new identity.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <param name="receiveTime">The read stamp of the session message</param>
    /// <returns>The session start message</returns>
    public static ContractSessionStart ToSessionStart(Guid profileId, DateTimeOffset receiveTime)
    {
        return new ContractSessionStart
               {
                   Id = Guid.NewGuid(),
                   ProfileId = profileId,
                   ReceiveTime = receiveTime.ToUniversalTime()
               };
    }

    /// <summary>
    /// Creates a session end message with a new identity.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <param name="receiveTime">The read stamp of the session message</param>
    /// <returns>The session end message</returns>
    public static ContractSessionEnd ToSessionEnd(Guid profileId, DateTimeOffset receiveTime)
    {
        return new ContractSessionEnd
               {
                   Id = Guid.NewGuid(),
                   ProfileId = profileId,
                   ReceiveTime = receiveTime.ToUniversalTime()
               };
    }

    /// <summary>
    /// Converts the pipe connection state into the contract's pipe state.
    /// </summary>
    /// <param name="state">The pipe connection state</param>
    /// <returns>The status message</returns>
    public static ContractStatus ToStatus(PipeConnectionState state)
    {
        return new ContractStatus
               {
                   PipeState = state switch
                               {
                                   PipeConnectionState.WaitingForGame => ContractPipeState.WaitingForGame,
                                   PipeConnectionState.Connected => ContractPipeState.Connected,
                                   PipeConnectionState.Receiving => ContractPipeState.Receiving,
                                   PipeConnectionState.PipeBusy => ContractPipeState.PipeBusy,
                                   PipeConnectionState.ProtocolMismatch => ContractPipeState.ProtocolMismatch,
                                   PipeConnectionState.Error => ContractPipeState.Error,
                                   _ => throw new ArgumentOutOfRangeException(nameof(state), state, "The pipe connection state has no contract pipe state.")
                               }
               };
    }

    /// <summary>
    /// Converts a pipe island into a contract island.
    /// </summary>
    /// <param name="island">The pipe island</param>
    /// <returns>The contract island</returns>
    private static ContractTickIsland ToIsland(PipeTickIsland island)
    {
        return new ContractTickIsland
               {
                   SessionGuid = island.Key.SessionGuid,
                   IslandId = island.Key.IslandId,
                   AreaIndex = island.Key.AreaIndex,
                   SessionId = island.SessionId,
                   Name = island.Name,
                   Entries = [.. island.Entries.Select(ToEntry)]
               };
    }

    /// <summary>
    /// Converts a production entry into a contract entry; a float that is not finite becomes no value.
    /// </summary>
    /// <param name="entry">The production entry</param>
    /// <returns>The contract entry</returns>
    private static ContractTickEntry ToEntry(ProductionEntry entry)
    {
        return new ContractTickEntry
               {
                   ProductGuid = entry.ProductGuid,
                   Generation = ToValue(entry.Generation),
                   Consumption = ToValue(entry.Consumption),
                   Delta = ToValue(entry.Delta),
                   PerfectGeneration = ToValue(entry.PerfectGeneration),
                   PerfectConsumption = ToValue(entry.PerfectConsumption),
                   BuildingCount = entry.BuildingCount,
                   Maintenance = entry.Maintenance,
                   Income = ToValue(entry.Income),
                   Profit = entry.Profit,
                   SummedProductivity = ToValue(entry.SummedProductivity),
                   AverageProductivity = ToValue(entry.AverageProductivity),
                   WorkforceAmounts = entry.WorkforceAmounts,
                   BuildingAmounts = entry.BuildingAmounts
               };
    }

    /// <summary>
    /// Converts a float: infinity and NaN cross as no value.
    /// </summary>
    /// <param name="value">The float</param>
    /// <returns>The float, or <c>null</c> when it is not finite</returns>
    private static float? ToValue(float value)
    {
        return float.IsFinite(value) ? value : null;
    }

    #endregion // Methods
}