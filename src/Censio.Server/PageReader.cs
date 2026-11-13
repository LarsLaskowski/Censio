namespace Censio.Server;

/// <summary>
/// Reads the view of a profile for a page, one read at a time.
/// </summary>
public sealed class PageReader
{
    #region Fields

    /// <summary>
    /// The source of the profile states.
    /// </summary>
#pragma warning disable S4487 // Skeleton: removed with the implementation
    private readonly IProfileStateSource _source;
#pragma warning restore S4487

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
#pragma warning disable S1172, S2325, RH2003 // Skeleton: removed with the implementation
    public Task<PageRead> ReadAsync(Guid profileId, IslandKey? island, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
#pragma warning restore S1172, S2325, RH2003

    #endregion // Methods
}