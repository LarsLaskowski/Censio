using Censio.Contracts;
using Censio.Server.Data;

namespace Censio.Server;

/// <summary>
/// The state of the server: the profiles, the held profile states and the catalog, over the store.
/// </summary>
public sealed partial class ServerState
{
    #region Constants

    /// <summary>
    /// The maximum number of profile states held in memory.
    /// </summary>
    public const int MaxHeldProfiles = 2;

    #endregion // Constants

    #region Fields

#pragma warning disable S4487 // Skeleton: removed with the implementation
    /// <summary>
    /// The store.
    /// </summary>
    private readonly ServerStore _store;
#pragma warning restore S4487

#pragma warning disable S4487 // Skeleton: removed with the implementation
    /// <summary>
    /// The logger.
    /// </summary>
    private readonly ILogger _logger;
#pragma warning restore S4487

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerState"/> class.
    /// </summary>
    /// <param name="store">The store</param>
    /// <param name="logger">The logger</param>
    public ServerState(ServerStore store, ILogger logger)
    {
        _store = store;
        _logger = logger;
    }

    #endregion // Constructors

    #region Events

#pragma warning disable CS0067, S3264 // Skeleton: removed with the implementation
    /// <summary>
    /// Occurs after the state changed.
    /// </summary>
    public event EventHandler<ServerStateChangedEventArgs>? Changed;
#pragma warning restore CS0067, S3264

    #endregion // Events

    #region Properties

#pragma warning disable S2325, CA1065, RH2003 // Skeleton: removed with the implementation
    /// <summary>
    /// Gets the profiles, ordered by identity.
    /// </summary>
    public IReadOnlyList<StoredProfile> Profiles => throw new NotImplementedException();

    /// <summary>
    /// Gets the identities of the held profiles.
    /// </summary>
    public IReadOnlyCollection<Guid> HeldProfiles => throw new NotImplementedException();

    /// <summary>
    /// Gets the identity of the catalog while it is usable and all its icons are stored, otherwise <see langword="null"/>.
    /// </summary>
    public string? CompleteCatalogIdentity => throw new NotImplementedException();
#pragma warning restore S2325, CA1065, RH2003

    #endregion // Properties

    #region Methods

#pragma warning disable S2325, RH2003 // Skeleton: removed with the implementation
    /// <summary>
    /// Restores the state from the store.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The number of profiles restored</returns>
    public Task<int> RestoreAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Reads the state of a profile.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The read result</returns>
    public Task<ProfileStateRead> ReadProfileStateAsync(Guid profileId, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Stores a profile.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <param name="name">The profile name</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The result of the store</returns>
    public Task<ServerStoreResult> StoreProfileAsync(Guid profileId, string name, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Stores a tick.
    /// </summary>
    /// <param name="tick">The tick</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The result of the store</returns>
    public Task<ServerStoreResult> StoreTickAsync(ContractTick tick, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Stores a catalog.
    /// </summary>
    /// <param name="upload">The catalog upload</param>
    /// <param name="document">The document of the upload</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The result of the store</returns>
    public Task<CatalogStoreResult> StoreCatalogAsync(ContractCatalogUpload upload, CatalogDocument document, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Stores an icon.
    /// </summary>
    /// <param name="icon">The icon</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The result of the store</returns>
    public Task<ServerStoreResult> StoreIconAsync(ContractIconImage icon, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
#pragma warning restore S2325, RH2003

    #endregion // Methods
}