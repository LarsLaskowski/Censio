using System.Net;

namespace Censio.Server;

/// <summary>
/// Holds the limits of the ingest endpoint per host: the pending hellos, the failed hellos and the block.
/// </summary>
public sealed partial class IngestHosts : IDisposable
{
    #region Constants

    /// <summary>
    /// The maximum number of connections of one host that wait for their hello at the same time.
    /// </summary>
    public const int MaxPendingHellosPerHost = 4;

    /// <summary>
    /// The number of failed hellos of one host within <see cref="FailureWindow"/> that blocks the host.
    /// </summary>
    public const int MaxFailedHellos = 10;

    /// <summary>
    /// The maximum number of hosts that have an entry of their own.
    /// </summary>
    public const int MaxTrackedHosts = 1024;

    #endregion // Constants

    #region Fields

#pragma warning disable S4487 // Skeleton: removed with the implementation

    /// <summary>
    /// The time source of the window and the block.
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// The logger of the block lines.
    /// </summary>
    private readonly ILogger _logger;

#pragma warning restore S4487

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="IngestHosts"/> class.
    /// </summary>
    /// <param name="timeProvider">The time source of the window and the block</param>
    /// <param name="logger">The logger of the block lines</param>
    public IngestHosts(TimeProvider timeProvider, ILogger logger)
    {
        _timeProvider = timeProvider;
        _logger = logger;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the window in which failed hellos of one host are counted.
    /// </summary>
    public static TimeSpan FailureWindow { get; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Gets the time a host stays blocked.
    /// </summary>
    public static TimeSpan BlockDuration { get; } = TimeSpan.FromSeconds(60);

    #endregion // Properties

    #region Methods

#pragma warning disable S2325, RH2003 // Skeleton: removed with the implementation

    /// <summary>
    /// Tries to count a connection of a host that waits for its hello.
    /// </summary>
    /// <param name="address">The address of the TCP peer, or <c>null</c> when it is unknown</param>
    /// <returns>The entry of the host when the connection was counted; otherwise <c>null</c></returns>
    public IngestHost? TryEnterPendingHello(IPAddress? address)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Releases a connection of a host that waited for its hello.
    /// </summary>
    /// <param name="host">The entry the connection was counted on</param>
    public void LeavePendingHello(IngestHost host)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Counts a failed hello of a host and blocks the host at <see cref="MaxFailedHellos"/>.
    /// </summary>
    /// <param name="host">The entry the connection was counted on</param>
    public void RecordFailedHello(IngestHost host)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Records an accepted hello of a host and gives the host an entry of its own.
    /// </summary>
    /// <param name="host">The entry the connection was counted on</param>
    /// <param name="address">The address of the TCP peer, or <c>null</c> when it is unknown</param>
    public void RecordAcceptedHello(IngestHost host, IPAddress? address)
    {
        throw new NotImplementedException();
    }

#pragma warning restore S2325, RH2003

    #endregion // Methods

    #region IDisposable

#pragma warning disable RH2003, CA1065 // Skeleton: removed with the implementation

    /// <inheritdoc />
    public void Dispose()
    {
        throw new NotImplementedException();
    }

#pragma warning restore RH2003, CA1065

    #endregion // IDisposable
}