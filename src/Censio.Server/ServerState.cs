using System.Collections.Immutable;

using Censio.Contracts;
using Censio.Server.Data;

using Microsoft.Data.Sqlite;

namespace Censio.Server;

/// <summary>
/// The state of the server: the profiles, the held profile states and the catalog, over the store.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001", Justification = "The semaphore is never waited on through its handle, so it holds nothing to dispose")]
public sealed partial class ServerState : IProfileStateSource
{
    #region Constants

    /// <summary>
    /// The maximum number of profile states held in memory.
    /// </summary>
    public const int MaxHeldProfiles = 2;

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The store.
    /// </summary>
    private readonly ServerStore _store;

    /// <summary>
    /// The logger.
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// The lock of the snapshot swap.
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>
    /// The gate that lets one store call at a time change the state, so that the storing order and the state agree.
    /// </summary>
    private readonly SemaphoreSlim _writeGate = new(1, 1);

    /// <summary>
    /// The profiles whose latest tick may be newer than every later tick: their island read failed. Guarded by the gate.
    /// </summary>
    private readonly HashSet<Guid> _markedProfiles = [];

    /// <summary>
    /// The current snapshot, replaced whole under the lock.
    /// </summary>
    private ServerStateSnapshot _snapshot = new()
                                            {
                                                Profiles = [],
                                                Held = new Dictionary<Guid, HeldProfileState>(),
                                                Catalog = CatalogHold.None
                                            };

    /// <summary>
    /// The last storing sequence. Guarded by the gate.
    /// </summary>
    private long _sequence;

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

    /// <summary>
    /// Occurs after the state changed.
    /// </summary>
#pragma warning disable S3264 // Raised follower by follower in Notify, each on its own work item
    public event EventHandler<ServerStateChangedEventArgs>? Changed;
#pragma warning restore S3264

    #endregion // Events

    #region Properties

    /// <summary>
    /// Gets the profiles, ordered by identity.
    /// </summary>
    public IReadOnlyList<StoredProfile> Profiles => GetSnapshot().Profiles;

    /// <summary>
    /// Gets the identities of the held profiles.
    /// </summary>
    public IReadOnlyCollection<Guid> HeldProfiles => [.. GetSnapshot().Held.Keys];

    /// <summary>
    /// Gets the identity of the catalog while it is usable and all its icons are stored, otherwise <see langword="null"/>.
    /// </summary>
    public string? CompleteCatalogIdentity
    {
        get
        {
            var catalog = GetSnapshot().Catalog;

            return catalog.IsUsable && catalog.MissingIcons!.IsEmpty
                       ? catalog.Identity
                       : null;
        }
    }

    /// <summary>
    /// Gets the state of the Companion's connection.
    /// </summary>
#pragma warning disable S2325 // Skeleton: removed with the implementation
    public CompanionConnection CompanionState => NotImplemented<CompanionConnection>();
#pragma warning restore S2325

    /// <summary>
    /// Gets the identity of the profile whose latest tick is the newest, or <see langword="null"/> when none is held.
    /// </summary>
#pragma warning disable S2325 // Skeleton: removed with the implementation
    public Guid? NewestProfile => NotImplemented<Guid?>();
#pragma warning restore S2325

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Restores the state from the store.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The number of profiles restored</returns>
    public async Task<int> RestoreAsync(CancellationToken cancellationToken)
    {
        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var profiles = await _store.ListProfilesAsync(cancellationToken).ConfigureAwait(false);
            var latest = await _store.ListLatestProfilesAsync(MaxHeldProfiles, cancellationToken).ConfigureAwait(false);
            var held = new Dictionary<Guid, HeldProfileState>();
            var sequence = 0L;

            foreach (var profileId in latest.Reverse())
            {
                var state = await _store.ReadProfileStateAsync(profileId, cancellationToken).ConfigureAwait(false);

                if (state?.LatestTick is { } tick)
                {
                    held[profileId] = new HeldProfileState
                                      {
                                          State = state,
                                          ReceiveTicks = tick.ReceiveTime.UtcTicks,
                                          Sequence = ++sequence
                                      };
                }
            }

            var catalog = await RestoreCatalogAsync(cancellationToken).ConfigureAwait(false);

            _sequence = sequence;
            _markedProfiles.Clear();
            Swap(new ServerStateSnapshot
                 {
                     Profiles = [.. profiles.OrderBy(profile => profile.Id.ToString(), StringComparer.Ordinal)],
                     Held = held,
                     Catalog = catalog
                 });

            return profiles.Count;
        }
        finally
        {
            _writeGate.Release();
        }
    }

    /// <summary>
    /// Stores a profile.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <param name="name">The profile name</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The result of the store</returns>
    public async Task<ServerStoreResult> StoreProfileAsync(Guid profileId, string name, CancellationToken cancellationToken)
    {
        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var result = await _store.StoreProfileAsync(profileId, name, cancellationToken).ConfigureAwait(false);

            if (result.Outcome != ServerStoreOutcome.Stored)
            {
                return result;
            }

            var snapshot = GetSnapshot();
            var profile = new StoredProfile
                          {
                              Id = profileId,
                              Name = name
                          };
            var held = snapshot.Held;

            if (held.TryGetValue(profileId, out var current))
            {
                held = Replace(held,
                               profileId,
                               new HeldProfileState
                               {
                                   State = new StoredProfileState
                                           {
                                               Profile = profile,
                                               LatestTick = current.State.LatestTick,
                                               Islands = current.State.Islands
                                           },
                                   ReceiveTicks = current.ReceiveTicks,
                                   Sequence = current.Sequence
                               });
            }

            Swap(new ServerStateSnapshot
                 {
                     Profiles = PutProfile(snapshot.Profiles, profile),
                     Held = held,
                     Catalog = snapshot.Catalog
                 });
            Notify(ServerStateChangeKind.Profile, profileId);

            return result;
        }
        finally
        {
            _writeGate.Release();
        }
    }

    /// <summary>
    /// Stores a tick.
    /// </summary>
    /// <param name="tick">The tick</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The result of the store</returns>
    public async Task<ServerStoreResult> StoreTickAsync(ContractTick tick, CancellationToken cancellationToken)
    {
        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var result = await _store.StoreTickAsync(tick, cancellationToken).ConfigureAwait(false);

            if (result.Outcome != ServerStoreOutcome.Stored)
            {
                return result;
            }

            var snapshot = GetSnapshot();
            var profiles = snapshot.Profiles;
            var index = FindProfile(profiles, tick.ProfileId);

            if (index < 0)
            {
                profiles = PutProfile(profiles,
                                      new StoredProfile
                                      {
                                          Id = tick.ProfileId,
                                          Name = null
                                      });
            }

            Swap(new ServerStateSnapshot
                 {
                     Profiles = profiles,
                     Held = await HoldTickAsync(tick, profiles, snapshot.Held).ConfigureAwait(false),
                     Catalog = snapshot.Catalog
                 });
            Notify(ServerStateChangeKind.Profile, tick.ProfileId);

            return result;
        }
        finally
        {
            _writeGate.Release();
        }
    }

    /// <summary>
    /// Stores a catalog.
    /// </summary>
    /// <param name="upload">The catalog upload</param>
    /// <param name="document">The document of the upload</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The result of the store</returns>
    public async Task<CatalogStoreResult> StoreCatalogAsync(ContractCatalogUpload upload, CatalogDocument document, CancellationToken cancellationToken)
    {
        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var snapshot = GetSnapshot();
            var identity = upload.ComputeIdentity();

            if (snapshot.Catalog.IsUsable && snapshot.Catalog.Identity == identity)
            {
                return new CatalogStoreResult
                       {
                           Result = new ServerStoreResult(ServerStoreOutcome.AlreadyStored, null),
                           MissingIcons = [.. snapshot.Catalog.MissingIcons!.Order(StringComparer.Ordinal)]
                       };
            }

            var result = await _store.StoreCatalogAsync(upload, cancellationToken).ConfigureAwait(false);

            if (result.Outcome != ServerStoreOutcome.Stored)
            {
                return new CatalogStoreResult
                       {
                           Result = result,
                           MissingIcons = []
                       };
            }

            try
            {
                var missing = await _store.FindMissingIconsAsync(GetReferences(document), CancellationToken.None).ConfigureAwait(false);

                SwapCatalog(snapshot,
                            new CatalogHold
                            {
                                Identity = identity,
                                MissingIcons = [.. missing],
                                Lookup = null
                            });

                return new CatalogStoreResult
                       {
                           Result = result,
                           MissingIcons = missing
                       };
            }
            catch (SqliteException exception)
            {
                SwapCatalog(snapshot, CatalogHold.None);

                return new CatalogStoreResult
                       {
                           Result = new ServerStoreResult(ServerStoreOutcome.Failed, exception),
                           MissingIcons = []
                       };
            }
        }
        finally
        {
            _writeGate.Release();
        }
    }

    /// <summary>
    /// Sets the Companion as connected.
    /// </summary>
#pragma warning disable S2325, RH2003 // Skeleton: removed with the implementation
    public void SetCompanionConnected()
    {
        throw new NotImplementedException();
    }
#pragma warning restore S2325, RH2003

    /// <summary>
    /// Sets the state of the Companion's pipe.
    /// </summary>
    /// <param name="pipeState">The pipe state</param>
#pragma warning disable S1172, S2325, RH2003 // Skeleton: removed with the implementation
    public void SetCompanionPipeState(ContractPipeState pipeState)
    {
        throw new NotImplementedException();
    }
#pragma warning restore S1172, S2325, RH2003

    /// <summary>
    /// Sets the Companion as disconnected.
    /// </summary>
#pragma warning disable S2325, RH2003 // Skeleton: removed with the implementation
    public void SetCompanionDisconnected()
    {
        throw new NotImplementedException();
    }
#pragma warning restore S2325, RH2003

    /// <summary>
    /// Reads the bytes of a stored icon.
    /// </summary>
    /// <param name="reference">The icon reference</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The icon bytes, or <see langword="null"/> when the icon is not stored</returns>
#pragma warning disable S1172, S2325, RH2003 // Skeleton: removed with the implementation
    public Task<byte[]?> ReadIconAsync(string reference, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
#pragma warning restore S1172, S2325, RH2003

    /// <summary>
    /// Stores an icon.
    /// </summary>
    /// <param name="icon">The icon</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The result of the store</returns>
    public async Task<ServerStoreResult> StoreIconAsync(ContractIconImage icon, CancellationToken cancellationToken)
    {
        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var result = await _store.StoreIconAsync(icon, cancellationToken).ConfigureAwait(false);

            if (result.Outcome != ServerStoreOutcome.Stored)
            {
                return result;
            }

            var snapshot = GetSnapshot();
            var catalog = snapshot.Catalog;

            SwapCatalog(snapshot,
                        catalog.IsUsable
                            ? new CatalogHold
                              {
                                  Identity = catalog.Identity,
                                  MissingIcons = catalog.MissingIcons!.Remove(icon.Reference),
                                  Lookup = catalog.Lookup
                              }
                            : catalog);

            return result;
        }
        finally
        {
            _writeGate.Release();
        }
    }

    /// <summary>
    /// Raises the skeleton's exception for a property.
    /// </summary>
    /// <typeparam name="T">The type of the property</typeparam>
    /// <returns>Never returns</returns>
#pragma warning disable RH2003 // Skeleton: removed with the implementation
    private static T NotImplemented<T>()
    {
        throw new NotImplementedException();
    }
#pragma warning restore RH2003

    /// <summary>
    /// Gets the distinct references of the icons of a catalog document, ordinally sorted.
    /// </summary>
    /// <param name="document">The document</param>
    /// <returns>The references</returns>
    private static List<string> GetReferences(CatalogDocument document)
    {
        return [.. document.Entries.Select(entry => entry.IconReference).OfType<string>().Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
    }

    /// <summary>
    /// Finds a profile in the profiles, which are ordered by identity.
    /// </summary>
    /// <param name="profiles">The profiles</param>
    /// <param name="profileId">The profile identity</param>
    /// <returns>The index, or the complement of the index where the profile belongs</returns>
    private static int FindProfile(IReadOnlyList<StoredProfile> profiles, Guid profileId)
    {
        var key = profileId.ToString();
        var low = 0;
        var high = profiles.Count - 1;

        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            var comparison = string.CompareOrdinal(profiles[middle].Id.ToString(), key);

            if (comparison == 0)
            {
                return middle;
            }

            if (comparison < 0)
            {
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return ~low;
    }

    /// <summary>
    /// Gives the profiles with a profile added or replaced, in identity order.
    /// </summary>
    /// <param name="profiles">The profiles</param>
    /// <param name="profile">The profile</param>
    /// <returns>The new list</returns>
    private static List<StoredProfile> PutProfile(IReadOnlyList<StoredProfile> profiles, StoredProfile profile)
    {
        var list = new List<StoredProfile>(profiles);
        var index = FindProfile(profiles, profile.Id);

        if (index >= 0)
        {
            list[index] = profile;
        }
        else
        {
            list.Insert(~index, profile);
        }

        return list;
    }

    /// <summary>
    /// Gives the held states with one replaced.
    /// </summary>
    /// <param name="held">The held states</param>
    /// <param name="profileId">The profile identity</param>
    /// <param name="state">The new state</param>
    /// <returns>The new held states</returns>
    private static Dictionary<Guid, HeldProfileState> Replace(IReadOnlyDictionary<Guid, HeldProfileState> held, Guid profileId, HeldProfileState state)
    {
        var copy = new Dictionary<Guid, HeldProfileState>(held)
                   {
                       [profileId] = state
                   };

        return copy;
    }

    /// <summary>
    /// Gives the held states with a state added, replacing the oldest one when all places are taken.
    /// </summary>
    /// <param name="held">The held states</param>
    /// <param name="profileId">The profile identity</param>
    /// <param name="state">The new state</param>
    /// <returns>The new held states</returns>
    private static Dictionary<Guid, HeldProfileState> Enter(IReadOnlyDictionary<Guid, HeldProfileState> held, Guid profileId, HeldProfileState state)
    {
        var copy = new Dictionary<Guid, HeldProfileState>(held);

        if (copy.Count >= MaxHeldProfiles)
        {
            copy.Remove(copy.MinBy(pair => (pair.Value.ReceiveTicks, pair.Value.Sequence)).Key);
        }

        copy[profileId] = state;

        return copy;
    }

    /// <summary>
    /// Tells whether a latest tick with a receive time takes a place among the held states.
    /// </summary>
    /// <param name="held">The held states</param>
    /// <param name="receiveTicks">The receive time of the tick, in UTC ticks</param>
    /// <returns><see langword="true"/> when a place is free or the tick is not older than the oldest held state</returns>
    private static bool Admits(IReadOnlyDictionary<Guid, HeldProfileState> held, long receiveTicks)
    {
        return held.Count < MaxHeldProfiles
               || receiveTicks >= held.Values.Min(state => state.ReceiveTicks);
    }

    /// <summary>
    /// Merges the islands of a newer tick into the listed islands: the tick's names win.
    /// </summary>
    /// <param name="islands">The listed islands</param>
    /// <param name="tick">The tick</param>
    /// <returns>The islands, ordered by key</returns>
    private static List<StoredIsland> MergeIslands(IReadOnlyList<StoredIsland> islands, ContractTick tick)
    {
        var merged = islands.ToDictionary(island => (island.SessionGuid, island.IslandId, island.AreaIndex));

        foreach (var island in tick.Islands)
        {
            merged[(island.SessionGuid, island.IslandId, island.AreaIndex)] = new StoredIsland
                                                                              {
                                                                                  SessionGuid = island.SessionGuid,
                                                                                  IslandId = island.IslandId,
                                                                                  AreaIndex = island.AreaIndex,
                                                                                  Name = island.Name
                                                                              };
        }

        return [.. merged.Values.OrderBy(island => island.SessionGuid).ThenBy(island => island.IslandId).ThenBy(island => island.AreaIndex)];
    }

    /// <summary>
    /// Logs a profile state that cannot be read.
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="exception">The exception behind the failure</param>
    [LoggerMessage(Level = LogLevel.Error, Message = "The current state of a profile cannot be read from the database.")]
    private static partial void LogStateReadFailed(ILogger logger, Exception exception);

    /// <summary>
    /// Logs a follower that failed.
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="exception">The exception of the follower</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "A follower of the current state failed.")]
    private static partial void LogFollowerFailed(ILogger logger, Exception exception);

    /// <summary>
    /// Gets the current snapshot.
    /// </summary>
    /// <returns>The snapshot</returns>
    private ServerStateSnapshot GetSnapshot()
    {
        lock (_lock)
        {
            return _snapshot;
        }
    }

    /// <summary>
    /// Replaces the snapshot.
    /// </summary>
    /// <param name="snapshot">The new snapshot</param>
    private void Swap(ServerStateSnapshot snapshot)
    {
        lock (_lock)
        {
            _snapshot = snapshot;
        }
    }

    /// <summary>
    /// Replaces the catalog of a snapshot and tells the followers.
    /// </summary>
    /// <param name="snapshot">The snapshot the catalog belongs to</param>
    /// <param name="catalog">The new catalog</param>
    private void SwapCatalog(ServerStateSnapshot snapshot, CatalogHold catalog)
    {
        Swap(new ServerStateSnapshot
             {
                 Profiles = snapshot.Profiles,
                 Held = snapshot.Held,
                 Catalog = catalog
             });
        Notify(ServerStateChangeKind.Catalog, null);
    }

    /// <summary>
    /// Tells each follower of a change on a thread-pool work item of its own.
    /// </summary>
    /// <param name="kind">The kind of the change</param>
    /// <param name="profileId">The profile identity, or <see langword="null"/></param>
    private void Notify(ServerStateChangeKind kind, Guid? profileId)
    {
        if (Changed is not { } handlers)
        {
            return;
        }

        var arguments = new ServerStateChangedEventArgs(kind, profileId);

        foreach (var handler in handlers.GetInvocationList().Cast<EventHandler<ServerStateChangedEventArgs>>())
        {
            _ = Task.Run(() => Tell(handler, arguments));
        }
    }

    /// <summary>
    /// Tells one follower and logs its failure.
    /// </summary>
    /// <param name="handler">The follower</param>
    /// <param name="arguments">The arguments</param>
    private void Tell(EventHandler<ServerStateChangedEventArgs> handler, ServerStateChangedEventArgs arguments)
    {
        try
        {
            handler(this, arguments);
        }
        catch (Exception exception)
        {
            LogFollowerFailed(_logger, exception);
        }
    }

    /// <summary>
    /// Reads the stored catalog for the restore.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The catalog hold</returns>
    private async Task<CatalogHold> RestoreCatalogAsync(CancellationToken cancellationToken)
    {
        var catalog = await _store.ReadCatalogAsync(cancellationToken).ConfigureAwait(false);

        if (catalog is null)
        {
            return CatalogHold.None;
        }

        var document = CatalogDocumentReader.Read(catalog.Document.Span).Document;

        if (document is null)
        {
            return CatalogHold.None;
        }

        var missing = await _store.FindMissingIconsAsync(GetReferences(document), cancellationToken).ConfigureAwait(false);

        return new CatalogHold
               {
                   Identity = catalog.Identity,
                   MissingIcons = [.. missing],
                   Lookup = null
               };
    }

    /// <summary>
    /// Decides how a stored tick changes the held states.
    /// </summary>
    /// <param name="tick">The stored tick</param>
    /// <param name="profiles">The profiles, with the tick's profile</param>
    /// <param name="held">The held states</param>
    /// <returns>The new held states</returns>
    private async Task<IReadOnlyDictionary<Guid, HeldProfileState>> HoldTickAsync(ContractTick tick, IReadOnlyList<StoredProfile> profiles, IReadOnlyDictionary<Guid, HeldProfileState> held)
    {
        var receiveTicks = tick.ReceiveTime.UtcTicks;

        if (held.TryGetValue(tick.ProfileId, out var current))
        {
            if (receiveTicks >= current.ReceiveTicks)
            {
                return Replace(held,
                               tick.ProfileId,
                               new HeldProfileState
                               {
                                   State = new StoredProfileState
                                           {
                                               Profile = current.State.Profile,
                                               LatestTick = tick,
                                               Islands = MergeIslands(current.State.Islands, tick)
                                           },
                                   ReceiveTicks = receiveTicks,
                                   Sequence = ++_sequence
                               });
            }

            var listed = await TryListIslandsAsync(tick.ProfileId).ConfigureAwait(false);

            return listed is null
                       ? held
                       : Replace(held,
                                 tick.ProfileId,
                                 new HeldProfileState
                                 {
                                     State = new StoredProfileState
                                             {
                                                 Profile = current.State.Profile,
                                                 LatestTick = current.State.LatestTick,
                                                 Islands = listed
                                             },
                                     ReceiveTicks = current.ReceiveTicks,
                                     Sequence = current.Sequence
                                 });
        }

        if (_markedProfiles.Contains(tick.ProfileId))
        {
            return await HoldStoredLatestAsync(tick.ProfileId, held).ConfigureAwait(false);
        }

        if (Admits(held, receiveTicks) == false)
        {
            return held;
        }

        var islands = await TryListIslandsAsync(tick.ProfileId).ConfigureAwait(false);

        if (islands is null)
        {
            _markedProfiles.Add(tick.ProfileId);

            return held;
        }

        return Enter(held,
                     tick.ProfileId,
                     new HeldProfileState
                     {
                         State = new StoredProfileState
                                 {
                                     Profile = profiles[FindProfile(profiles, tick.ProfileId)],
                                     LatestTick = tick,
                                     Islands = islands
                                 },
                         ReceiveTicks = receiveTicks,
                         Sequence = ++_sequence
                     });
    }

    /// <summary>
    /// Holds the stored latest state of a marked profile, when it takes a place.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <param name="held">The held states</param>
    /// <returns>The new held states</returns>
    private async Task<IReadOnlyDictionary<Guid, HeldProfileState>> HoldStoredLatestAsync(Guid profileId, IReadOnlyDictionary<Guid, HeldProfileState> held)
    {
        StoredProfileState? stored;

        try
        {
            stored = await _store.ReadProfileStateAsync(profileId, CancellationToken.None).ConfigureAwait(false);
        }
        catch (SqliteException exception)
        {
            LogStateReadFailed(_logger, exception);

            return held;
        }

        _markedProfiles.Remove(profileId);

        if (stored?.LatestTick is not { } latest || Admits(held, latest.ReceiveTime.UtcTicks) == false)
        {
            return held;
        }

        return Enter(held,
                     profileId,
                     new HeldProfileState
                     {
                         State = stored,
                         ReceiveTicks = latest.ReceiveTime.UtcTicks,
                         Sequence = ++_sequence
                     });
    }

    /// <summary>
    /// Lists the islands of a profile after its tick was stored; a failure is logged. The read ignores a cancellation
    /// because the tick is stored already and the state must follow it.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <returns>The islands, or <see langword="null"/> when the read failed</returns>
    private async Task<IReadOnlyList<StoredIsland>?> TryListIslandsAsync(Guid profileId)
    {
        try
        {
            return await _store.ListIslandsAsync(profileId, CancellationToken.None).ConfigureAwait(false);
        }
        catch (SqliteException exception)
        {
            LogStateReadFailed(_logger, exception);

            return null;
        }
    }

    #endregion // Methods

    #region IProfileStateSource

    /// <inheritdoc/>
#pragma warning disable S2325 // Skeleton: removed with the implementation
    public CatalogHold Catalog => NotImplemented<CatalogHold>();
#pragma warning restore S2325

    /// <inheritdoc/>
#pragma warning disable S1172, S2325, RH2003 // Skeleton: removed with the implementation
    public bool TryGetHeld(Guid profileId, out StoredProfileState? state)
    {
        throw new NotImplementedException();
    }
#pragma warning restore S1172, S2325, RH2003

    /// <inheritdoc/>
    public async Task<ProfileStateRead> ReadProfileStateAsync(Guid profileId, CancellationToken cancellationToken)
    {
        var snapshot = GetSnapshot();

        if (snapshot.Held.TryGetValue(profileId, out var held))
        {
            return new ProfileStateRead
                   {
                       State = held.State,
                       Failure = null
                   };
        }

        if (FindProfile(snapshot.Profiles, profileId) < 0)
        {
            return new ProfileStateRead
                   {
                       State = null,
                       Failure = null
                   };
        }

        try
        {
            return new ProfileStateRead
                   {
                       State = await _store.ReadProfileStateAsync(profileId, cancellationToken).ConfigureAwait(false),
                       Failure = null
                   };
        }
        catch (SqliteException exception)
        {
            LogStateReadFailed(_logger, exception);

            return new ProfileStateRead
                   {
                       State = null,
                       Failure = exception
                   };
        }
    }

    #endregion // IProfileStateSource
}