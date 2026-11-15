using System.Net;

namespace Censio.Server;

/// <summary>
/// The places and the outcome report of one ingest connection.
/// </summary>
public sealed class IngestAdmission
{
    #region Fields

    /// <summary>
    /// The connection counters.
    /// </summary>
    private readonly IngestConnections _connections;

    /// <summary>
    /// The hosts of the endpoint.
    /// </summary>
    private readonly IngestHosts _hosts;

    /// <summary>
    /// The entry of the host of the connection.
    /// </summary>
    private readonly IngestHost _host;

    /// <summary>
    /// The address of the TCP peer, or <c>null</c> when it is unknown.
    /// </summary>
    private readonly IPAddress? _address;

    /// <summary>
    /// A value indicating whether the outcome of the hello was reported (0 not yet, 1 reported).
    /// </summary>
    private int _reported;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="IngestAdmission"/> class.
    /// </summary>
    /// <param name="connections">The connection counters</param>
    /// <param name="hosts">The hosts of the endpoint</param>
    /// <param name="host">The entry of the host of the connection</param>
    /// <param name="address">The address of the TCP peer, or <c>null</c> when it is unknown</param>
    public IngestAdmission(IngestConnections connections, IngestHosts hosts, IngestHost host, IPAddress? address)
    {
        _connections = connections;
        _hosts = hosts;
        _host = host;
        _address = address;
    }

    #endregion // Constructors

    #region Methods

    /// <summary>
    /// Releases the pending place of the connection.
    /// </summary>
    public void LeavePendingHello()
    {
        _connections.LeavePendingHello();
    }

    /// <summary>
    /// Tries to take the place of the one Companion.
    /// </summary>
    /// <returns><c>true</c> when the place was taken</returns>
    public bool TryTakeCompanion()
    {
        return _connections.TryTakeCompanion();
    }

    /// <summary>
    /// Releases the place of the Companion.
    /// </summary>
    public void ReleaseCompanion()
    {
        _connections.ReleaseCompanion();
    }

    /// <summary>
    /// Reports that the hello of the connection failed; the host's waiting place is left with it.
    /// </summary>
    public void RecordFailedHello()
    {
        if (TryReport())
        {
            _hosts.RecordFailedHello(_host);
        }
    }

    /// <summary>
    /// Reports that the hello of the connection was accepted; the host's waiting place is left with it.
    /// </summary>
    public void RecordAcceptedHello()
    {
        if (TryReport())
        {
            _hosts.RecordAcceptedHello(_host, _address);
        }
    }

    /// <summary>
    /// Reports that the hello of the connection ended without counting as failed or accepted; the host's waiting place is left.
    /// </summary>
    public void RecordUncountedHello()
    {
        if (TryReport())
        {
            _hosts.LeavePendingHello(_host);
        }
    }

    /// <summary>
    /// Takes the right to report the outcome, which only the first report has.
    /// </summary>
    /// <returns><c>true</c> when no outcome was reported before</returns>
    private bool TryReport()
    {
        return Interlocked.Exchange(ref _reported, 1) == 0;
    }

    #endregion // Methods
}