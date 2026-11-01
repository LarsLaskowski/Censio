namespace Censio.Companion.Core;

/// <summary>
/// Keeps the connection to the statistics pipe: connects, reads, reconnects and publishes its status.
/// </summary>
public sealed class PipeConnection
{
    #region Fields

    /// <summary>
    /// The connector that makes the attempts.
    /// </summary>
    private readonly IPipeConnector _connector;

    /// <summary>
    /// The clock of the retry delays and the connection time.
    /// </summary>
    private readonly TimeProvider _timeProvider;

    #endregion // Fields

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="PipeConnection" /> class.
    /// </summary>
    /// <param name="connector">The connector that makes the attempts</param>
    /// <param name="timeProvider">The clock</param>
    public PipeConnection(IPipeConnector connector, TimeProvider timeProvider)
    {
        _connector = connector;
        _timeProvider = timeProvider;
    }

    #endregion // Constructor

    #region Events

    /// <summary>
    /// Occurs when the status changes.
    /// </summary>
    public event EventHandler<PipeConnectionStatus>? StatusChanged;

    #endregion // Events

    #region Properties

    /// <summary>
    /// Gets the current status.
    /// </summary>
    public PipeConnectionStatus Status => throw new NotImplementedException();

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Connects and reconnects until the token is cancelled.
    /// </summary>
    /// <param name="cancellationToken">The token that stops the run</param>
    /// <returns>A task that completes when the run has stopped</returns>
    /// <exception cref="InvalidOperationException">A run is already in progress</exception>
    public Task RunAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    #endregion // Methods
}