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

#pragma warning disable S4487 // Skeleton: removed with the implementation

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

#pragma warning restore S4487

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

#pragma warning disable S2325, RH2003 // Skeleton: removed with the implementation

    /// <summary>
    /// Reports that the hello of the connection failed.
    /// </summary>
    public void RecordFailedHello()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Reports that the hello of the connection was accepted.
    /// </summary>
    public void RecordAcceptedHello()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Reports that the hello of the connection ended without counting as failed or accepted.
    /// </summary>
    public void RecordUncountedHello()
    {
        throw new NotImplementedException();
    }

#pragma warning restore S2325, RH2003

    #endregion // Methods
}