using Censio.Server.Data;

namespace Censio.Server;

/// <summary>
/// An immutable snapshot of the server state: replaced whole, never changed in place.
/// </summary>
internal sealed class ServerStateSnapshot
{
    #region Properties

    /// <summary>
    /// Gets the profiles, ordered by identity.
    /// </summary>
    public required IReadOnlyList<StoredProfile> Profiles { get; init; }

    /// <summary>
    /// Gets the held states by profile identity.
    /// </summary>
    public required IReadOnlyDictionary<Guid, HeldProfileState> Held { get; init; }

    /// <summary>
    /// Gets the catalog.
    /// </summary>
    public required CatalogHold Catalog { get; init; }

    #endregion // Properties
}