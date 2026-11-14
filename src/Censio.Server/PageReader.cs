using Censio.Server.Data;

namespace Censio.Server;

/// <summary>
/// Reads the view of a profile for a page, one read at a time.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001", Justification = "The semaphore is never waited on through its handle, so it holds nothing to dispose")]
public sealed class PageReader
{
    #region Fields

    /// <summary>
    /// The source of the profile states.
    /// </summary>
    private readonly IProfileStateSource _source;

    /// <summary>
    /// The gate that lets one read at a time reach the source.
    /// </summary>
    private readonly SemaphoreSlim _gate = new(1, 1);

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PageReader"/> class.
    /// </summary>
    /// <param name="source">The source of the profile states</param>
    public PageReader(IProfileStateSource source)
    {
        _source = source;
    }

    #endregion // Constructors

    #region Methods

    /// <summary>
    /// Reads the view of a profile.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <param name="island">The shown island, or <see langword="null"/> for none</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The read result</returns>
    public async Task<PageRead> ReadAsync(Guid profileId, IslandKey? island, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_source.TryGetHeld(profileId, out var held) && held is not null)
        {
            return Build(held, island);
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var read = await _source.ReadProfileStateAsync(profileId, cancellationToken).ConfigureAwait(false);

            if (read.State is { } state)
            {
                return Build(state, island);
            }

            return new PageRead
                   {
                       View = null,
                       IsFailed = read.Failure is not null
                   };
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Builds the view of a profile state with the catalog the source holds.
    /// </summary>
    /// <param name="state">The profile state</param>
    /// <param name="island">The shown island, or <see langword="null"/> for none</param>
    /// <returns>The read result</returns>
    private PageRead Build(StoredProfileState state, IslandKey? island)
    {
        var catalog = _source.Catalog;

        return new PageRead
               {
                   View = ProfileViewBuilder.Build(state, catalog.Lookup, catalog.MissingIcons, island),
                   IsFailed = false
               };
    }

    #endregion // Methods
}