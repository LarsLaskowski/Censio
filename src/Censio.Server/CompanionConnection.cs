using Censio.Contracts;

namespace Censio.Server;

/// <summary>
/// The state of the Companion's connection to the server and of its pipe.
/// </summary>
public sealed class CompanionConnection
{
    #region Properties

    /// <summary>
    /// Gets the state of no connected Companion.
    /// </summary>
    public static CompanionConnection Disconnected { get; } = new()
                                                              {
                                                                  IsConnected = false,
                                                                  PipeState = null
                                                              };

    /// <summary>
    /// Gets a value indicating whether a Companion is connected.
    /// </summary>
    public required bool IsConnected { get; init; }

    /// <summary>
    /// Gets the state of the Companion's pipe, or <see langword="null"/> when none is known.
    /// </summary>
    public required ContractPipeState? PipeState { get; init; }

    #endregion // Properties
}