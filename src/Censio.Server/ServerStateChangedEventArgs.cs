namespace Censio.Server;

/// <summary>
/// The arguments of a change of the server state.
/// </summary>
public sealed class ServerStateChangedEventArgs : EventArgs
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerStateChangedEventArgs"/> class.
    /// </summary>
    /// <param name="kind">The kind of the change</param>
    /// <param name="profileId">The identity of the changed profile, or <see langword="null"/> for another kind</param>
    public ServerStateChangedEventArgs(ServerStateChangeKind kind, Guid? profileId)
    {
        Kind = kind;
        ProfileId = profileId;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the kind of the change.
    /// </summary>
    public ServerStateChangeKind Kind { get; }

    /// <summary>
    /// Gets the identity of the changed profile, or <see langword="null"/> for another kind.
    /// </summary>
    public Guid? ProfileId { get; }

    #endregion // Properties
}