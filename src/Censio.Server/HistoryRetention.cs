using Censio.Server.Data;

namespace Censio.Server;

/// <summary>
/// Folds old history, clears deleted data and runs the deletion of a profile's history.
/// </summary>
public sealed partial class HistoryRetention : IDisposable
{
    #region Fields

    /// <summary>
    /// The time between the end of a run and the start of the next one
    /// </summary>
    public static readonly TimeSpan RunInterval = TimeSpan.FromHours(1);

    // Skeleton: removed with the implementation
#pragma warning disable S4487
    /// <summary>
    /// The state
    /// </summary>
    private readonly ServerState _state;

    /// <summary>
    /// The history store
    /// </summary>
    private readonly HistoryStore _store;

    /// <summary>
    /// The time provider
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// The logger
    /// </summary>
    private readonly ILogger _logger;
#pragma warning restore S4487

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="HistoryRetention"/> class.
    /// </summary>
    /// <param name="state">The state</param>
    /// <param name="store">The history store</param>
    /// <param name="timeProvider">The time provider</param>
    /// <param name="logger">The logger</param>
    public HistoryRetention(ServerState state, HistoryStore store, TimeProvider timeProvider, ILogger logger)
    {
        _state = state;
        _store = store;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    #endregion // Constructors

    #region Methods

    // Skeleton: removed with the implementation
#pragma warning disable S1172, S2325, RH2003
    /// <summary>
    /// Starts the retention loop.
    /// </summary>
    /// <param name="stoppingToken">The token that ends the loop</param>
    public void Start(CancellationToken stoppingToken)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Runs one retention run: sweeps deleted data, folds old ticks and clears the log.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The task</returns>
    public Task RunAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Deletes the history of a profile.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The outcome</returns>
    public Task<HistoryOutcome> DeleteProfileAsync(Guid profileId, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
#pragma warning restore S1172, S2325, RH2003

    #endregion // Methods

    #region IDisposable

    /// <inheritdoc/>
    public void Dispose()
    {
    }

    #endregion // IDisposable
}