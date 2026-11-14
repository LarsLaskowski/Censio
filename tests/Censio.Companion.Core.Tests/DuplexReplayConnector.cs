using System.Net.WebSockets;

namespace Censio.Companion.Core.Tests;

/// <summary>
/// An <see cref="IReplayServerConnector" /> that returns a given socket, or <c>null</c> for a server that cannot be reached,
/// and records the addresses it was asked for.
/// </summary>
public sealed class DuplexReplayConnector : IReplayServerConnector
{
    #region Fields

    /// <summary>
    /// The socket every connection returns, or <c>null</c>.
    /// </summary>
    private readonly WebSocket? _socket;

    /// <summary>
    /// The addresses the connector was asked for.
    /// </summary>
    private readonly List<Uri> _addresses = [];

    /// <summary>
    /// The lock of the addresses.
    /// </summary>
    private readonly Lock _lock = new();

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="DuplexReplayConnector" /> class.
    /// </summary>
    /// <param name="socket">The socket every connection returns, or <c>null</c> for a server that cannot be reached</param>
    public DuplexReplayConnector(WebSocket? socket)
    {
        _socket = socket;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the addresses the connector was asked for, in order.
    /// </summary>
    public IReadOnlyList<Uri> Addresses
    {
        get
        {
            lock (_lock)
            {
                return [.. _addresses];
            }
        }
    }

    #endregion // Properties

    #region IReplayServerConnector

    /// <inheritdoc />
    public Task<WebSocket?> ConnectAsync(Uri address, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            _addresses.Add(address);
        }

        return Task.FromResult(_socket);
    }

    #endregion // IReplayServerConnector
}