namespace Censio.Server.Data;

/// <summary>
/// The outcome of a write to the server store.
/// </summary>
public enum ServerStoreOutcome
{
    /// <summary>
    /// The data was stored.
    /// </summary>
    Stored,

    /// <summary>
    /// The data was already stored; nothing was written.
    /// </summary>
    AlreadyStored,

    /// <summary>
    /// The write failed; nothing was stored.
    /// </summary>
    Failed
}