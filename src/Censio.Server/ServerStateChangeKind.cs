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
    Catalog,

    /// <summary>
    /// The Companion's connection or pipe state changed.
    /// </summary>
    Companion,

    /// <summary>
    /// The trade routes of a profile changed.
    /// </summary>
    TradeRoutes,

    /// <summary>
    /// The icons of the catalog changed.
    /// </summary>
    Icons
}