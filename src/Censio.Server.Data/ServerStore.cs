using Censio.Contracts;

namespace Censio.Server.Data;

/// <summary>
/// Stores and reads the server's data in the server database.
/// </summary>
public sealed class ServerStore
{
    #region Fields

    /// <summary>
    /// The database
    /// </summary>
#pragma warning disable S4487 // Skeleton: removed with the implementation
    private readonly ServerDatabase _database;
#pragma warning restore S4487 // Skeleton: removed with the implementation

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerStore"/> class.
    /// </summary>
    /// <param name="database">The database</param>
    public ServerStore(ServerDatabase database)
    {
        _database = database;
    }

    #endregion // Constructors

    #region Methods

#pragma warning disable S1172, S2325, RH2003 // Skeleton: removed with the implementation

    /// <summary>
    /// Stores the name of a profile.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <param name="name">The profile name</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The store result</returns>
    public Task<ServerStoreResult> StoreProfileAsync(Guid profileId, string name, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Lists the stored profiles.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The profiles</returns>
    public Task<IReadOnlyList<StoredProfile>> ListProfilesAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Stores a tick.
    /// </summary>
    /// <param name="tick">The tick</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The store result</returns>
    public Task<ServerStoreResult> StoreTickAsync(ContractTick tick, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Reads a tick.
    /// </summary>
    /// <param name="tickId">The tick identity</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The tick, or <see langword="null"/> when unknown</returns>
    public Task<ContractTick?> ReadTickAsync(Guid tickId, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Reads the latest tick of a profile.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The tick, or <see langword="null"/> when none is stored</returns>
    public Task<ContractTick?> ReadLatestTickAsync(Guid profileId, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Lists the islands of a profile.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The islands</returns>
    public Task<IReadOnlyList<StoredIsland>> ListIslandsAsync(Guid profileId, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Stores the catalog, replacing an earlier one.
    /// </summary>
    /// <param name="upload">The catalog upload</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The store result</returns>
    public Task<ServerStoreResult> StoreCatalogAsync(ContractCatalogUpload upload, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Reads the stored catalog.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The catalog, or <see langword="null"/> when none is stored</returns>
    public Task<StoredCatalog?> ReadCatalogAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Stores an icon.
    /// </summary>
    /// <param name="icon">The icon</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The store result</returns>
    public Task<ServerStoreResult> StoreIconAsync(ContractIconImage icon, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Reads an icon.
    /// </summary>
    /// <param name="reference">The icon reference key</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The PNG bytes, or <see langword="null"/> when unknown</returns>
    public Task<byte[]?> ReadIconAsync(string reference, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Finds the references without a stored icon.
    /// </summary>
    /// <param name="references">The icon reference keys</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The missing references, distinct and ordinally sorted</returns>
    public Task<IReadOnlyList<string>> FindMissingIconsAsync(IReadOnlyCollection<string> references, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

#pragma warning restore S1172, S2325, RH2003 // Skeleton: removed with the implementation

    #endregion // Methods
}