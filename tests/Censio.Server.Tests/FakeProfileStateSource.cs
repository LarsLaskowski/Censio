using System.Collections.Concurrent;

using Censio.Server.Data;

namespace Censio.Server.Tests;

/// <summary>
/// An <see cref="IProfileStateSource" /> of held and stored states set by the test: it records each read, counts the reads
/// in flight, can hold every read until released, and can evict a held profile at the moment it is checked.
/// </summary>
public sealed class FakeProfileStateSource : IProfileStateSource, IDisposable
{
    #region Fields

    /// <summary>
    /// The held states.
    /// </summary>
    private readonly ConcurrentDictionary<Guid, StoredProfileState> _held = new();

    /// <summary>
    /// The results of the reads, by profile; a profile without one reads as unknown.
    /// </summary>
    private readonly ConcurrentDictionary<Guid, ProfileStateRead> _stored = new();

    /// <summary>
    /// The profiles evicted when they are checked.
    /// </summary>
    private readonly ConcurrentDictionary<Guid, bool> _evictOnCheck = new();

    /// <summary>
    /// The profiles read, in the order the reads started.
    /// </summary>
    private readonly ConcurrentQueue<Guid> _reads = new();

    /// <summary>
    /// Released once per started read.
    /// </summary>
    private readonly SemaphoreSlim _readStarted = new(0);

    /// <summary>
    /// Completed when the held reads may end, or <see langword="null" /> when reads end at once.
    /// </summary>
    private readonly TaskCompletionSource? _release;

    /// <summary>
    /// The number of reads in flight.
    /// </summary>
    private int _active;

    /// <summary>
    /// The greatest number of reads in flight at once.
    /// </summary>
    private int _maxActive;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeProfileStateSource" /> class.
    /// </summary>
    /// <param name="holdReads">Whether every read waits until <see cref="ReleaseReads" /></param>
    public FakeProfileStateSource(bool holdReads)
    {
        _release = holdReads
                       ? new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)
                       : null;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the profiles read, in the order the reads started.
    /// </summary>
    public IReadOnlyList<Guid> Reads => [.. _reads];

    /// <summary>
    /// Gets the greatest number of reads in flight at once.
    /// </summary>
    public int MaxActive => Volatile.Read(ref _maxActive);

    /// <summary>
    /// Gets or sets what the source knows of the catalog.
    /// </summary>
    public CatalogHold CatalogHold { get; set; } = CatalogHold.None;

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Holds a state.
    /// </summary>
    /// <param name="state">The state</param>
    public void Hold(StoredProfileState state)
    {
        _held[state.Profile.Id] = state;
    }

    /// <summary>
    /// Sets the result of a read of a profile.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <param name="read">The result</param>
    public void Store(Guid profileId, ProfileStateRead read)
    {
        _stored[profileId] = read;
    }

    /// <summary>
    /// Evicts a held profile at the moment it is next checked, so that the check finds it not held.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    public void EvictOnCheck(Guid profileId)
    {
        _evictOnCheck[profileId] = true;
    }

    /// <summary>
    /// Forgets the held and the stored state of a profile, so that the source holds no reference to it any more.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    public void Forget(Guid profileId)
    {
        _held.TryRemove(profileId, out _);
        _stored.TryRemove(profileId, out _);
    }

    /// <summary>
    /// Lets the held reads end.
    /// </summary>
    public void ReleaseReads()
    {
        _release?.TrySetResult();
    }

    /// <summary>
    /// Waits until a further read has started.
    /// </summary>
    /// <param name="timeout">The longest wait</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A task that completes when a read has started</returns>
    public async Task WaitForReadAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (await _readStarted.WaitAsync(timeout, cancellationToken).ConfigureAwait(false) == false)
        {
            throw new TimeoutException("No read started");
        }
    }

    /// <summary>
    /// Raises the greatest number of reads in flight to a count.
    /// </summary>
    /// <param name="active">The count</param>
    private void InterlockedMax(int active)
    {
        var current = Volatile.Read(ref _maxActive);

        while (active > current)
        {
            var seen = Interlocked.CompareExchange(ref _maxActive, active, current);

            if (seen == current)
            {
                return;
            }

            current = seen;
        }
    }

    #endregion // Methods

    #region IProfileStateSource

    /// <inheritdoc />
    public CatalogHold Catalog => CatalogHold;

    /// <inheritdoc />
    public bool TryGetHeld(Guid profileId, out StoredProfileState? state)
    {
        if (_evictOnCheck.TryRemove(profileId, out _))
        {
            _held.TryRemove(profileId, out _);
        }

        var isHeld = _held.TryGetValue(profileId, out var held);

        state = held;

        return isHeld;
    }

    /// <inheritdoc />
    public async Task<ProfileStateRead> ReadProfileStateAsync(Guid profileId, CancellationToken cancellationToken)
    {
        var active = Interlocked.Increment(ref _active);

        InterlockedMax(active);
        _reads.Enqueue(profileId);
        _readStarted.Release();

        try
        {
            if (_release is not null)
            {
                await _release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            return _stored.TryGetValue(profileId, out var read)
                       ? read
                       : new ProfileStateRead
                         {
                             State = null,
                             Failure = null
                         };
        }
        finally
        {
            Interlocked.Decrement(ref _active);
        }
    }

    #endregion // IProfileStateSource

    #region IDisposable

    /// <inheritdoc />
    public void Dispose()
    {
        _readStarted.Dispose();
    }

    #endregion // IDisposable
}