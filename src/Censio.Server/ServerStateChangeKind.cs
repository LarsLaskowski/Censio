namespace Censio.Server;

/// <summary>
/// The kind of a change of the server state.
/// </summary>
public enum ServerStateChangeKind
{
    /// <summary>
    /// A profile changed.
    /// </summary>
    Profile,

    /// <summary>
    /// The catalog changed.
    /// </summary>
    Catalog
}