using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Censio.Companion.Core.Tests;

/// <summary>
/// A Kestrel host on 127.0.0.1 with ports chosen by the system: a WebSocket endpoint, an HTTP answer, a redirect and a
/// held upgrade over plain HTTP, the WebSocket endpoint over TLS with a self-signed certificate created in memory (its key
/// never persisted, no certificate store written), and a recording proxy that counts the connections it receives.
/// </summary>
public sealed class LoopbackWebSocketHost : IAsyncDisposable
{
    #region Constants

    /// <summary>
    /// The path of the WebSocket endpoint.
    /// </summary>
    public const string IngestPath = "/ingest";

    /// <summary>
    /// The path that answers 404.
    /// </summary>
    public const string MissingPath = "/missing";

    /// <summary>
    /// The path that answers 200 with a page, as a server without the endpoint.
    /// </summary>
    public const string PagePath = "/page";

    /// <summary>
    /// The path that answers 302 to the WebSocket endpoint.
    /// </summary>
    public const string RedirectPath = "/moved";

    /// <summary>
    /// The path whose upgrade is held until the host stops.
    /// </summary>
    public const string HeldPath = "/held";

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The application.
    /// </summary>
    private readonly WebApplication _application;

    /// <summary>
    /// The certificate of the TLS endpoint.
    /// </summary>
    private readonly X509Certificate2 _certificate;

    /// <summary>
    /// The listener of the recording proxy.
    /// </summary>
    private readonly TcpListener _proxy;

    /// <summary>
    /// The headers of every request to the WebSocket endpoint, plain or TLS.
    /// </summary>
    private readonly ConcurrentQueue<IReadOnlyDictionary<string, string>> _endpointRequests = new();

    /// <summary>
    /// Completed when the held upgrade arrives.
    /// </summary>
    private readonly TaskCompletionSource _heldRequest = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Cancelled when the host stops.
    /// </summary>
    private readonly CancellationTokenSource _stopping = new();

    /// <summary>
    /// The loop that accepts the proxy's connections.
    /// </summary>
    private Task _proxyLoop = Task.CompletedTask;

    /// <summary>
    /// The number of connections the proxy received.
    /// </summary>
    private int _proxyConnections;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="LoopbackWebSocketHost" /> class.
    /// </summary>
    private LoopbackWebSocketHost()
    {
        _certificate = CreateCertificate();
        _proxy = new TcpListener(IPAddress.Loopback, 0);

        var builder = WebApplication.CreateSlimBuilder();

        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options =>
                                         {
                                             options.Listen(IPAddress.Loopback, 0);
                                             options.Listen(IPAddress.Loopback, 0, listen => listen.UseHttps(_certificate));
                                         });

        _application = builder.Build();
        _application.UseWebSockets();
        _application.Run(HandleAsync);
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the base address of the plain HTTP endpoint.
    /// </summary>
    public Uri HttpAddress { get; private set; } = new("http://127.0.0.1");

    /// <summary>
    /// Gets the base address of the TLS endpoint.
    /// </summary>
    public Uri TlsAddress { get; private set; } = new("https://127.0.0.1");

    /// <summary>
    /// Gets the address of the recording proxy.
    /// </summary>
    public Uri ProxyAddress { get; private set; } = new("http://127.0.0.1");

    /// <summary>
    /// Gets the headers of every request that reached the WebSocket endpoint, plain or TLS.
    /// </summary>
    public IReadOnlyList<IReadOnlyDictionary<string, string>> EndpointRequests => [.. _endpointRequests];

    /// <summary>
    /// Gets the number of connections the recording proxy received.
    /// </summary>
    public int ProxyConnections => Volatile.Read(ref _proxyConnections);

    /// <summary>
    /// Gets a task that completes when the held upgrade arrived.
    /// </summary>
    public Task HeldRequestReceived => _heldRequest.Task;

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Starts a host.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The started host</returns>
    public static async Task<LoopbackWebSocketHost> StartAsync(CancellationToken cancellationToken)
    {
        var host = new LoopbackWebSocketHost();

        await host._application.StartAsync(cancellationToken).ConfigureAwait(false);

        var addresses = host._application.Services.GetRequiredService<IServer>().Features.GetRequiredFeature<IServerAddressesFeature>().Addresses.Select(address => new Uri(address)).ToList();

        host.HttpAddress = addresses.Single(address => address.Scheme == Uri.UriSchemeHttp);
        host.TlsAddress = addresses.Single(address => address.Scheme == Uri.UriSchemeHttps);

        host._proxy.Start();
        host.ProxyAddress = new UriBuilder(Uri.UriSchemeHttp, IPAddress.Loopback.ToString(), ((IPEndPoint)host._proxy.LocalEndpoint).Port).Uri;
        host._proxyLoop = host.AcceptProxyConnectionsAsync();

        return host;
    }

    /// <summary>
    /// Returns the WebSocket address of a path.
    /// </summary>
    /// <param name="path">The path</param>
    /// <param name="isTls">Whether the TLS endpoint is meant</param>
    /// <returns>The address</returns>
    public Uri WebSocketAddress(string path, bool isTls)
    {
        var baseAddress = isTls ? TlsAddress : HttpAddress;

        return new UriBuilder(baseAddress)
               {
                   Scheme = isTls ? Uri.UriSchemeWss : Uri.UriSchemeWs,
                   Path = path
               }.Uri;
    }

    /// <summary>
    /// Creates a self-signed certificate for 127.0.0.1 in memory.
    /// </summary>
    /// <returns>The certificate with its key</returns>
    private static X509Certificate2 CreateCertificate()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=127.0.0.1", key, HashAlgorithmName.SHA256);
        var names = new SubjectAlternativeNameBuilder();

        names.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(names.Build());
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], false));

        var now = DateTimeOffset.UtcNow;

        return request.CreateSelfSigned(now.AddDays(-1), now.AddDays(1));
    }

    /// <summary>
    /// Answers a request by its path.
    /// </summary>
    /// <param name="context">The request's context</param>
    /// <returns>A task that completes when the request is answered</returns>
    private async Task HandleAsync(HttpContext context)
    {
        switch (context.Request.Path.Value)
        {
            case IngestPath:
                {
                    await HandleEndpointAsync(context).ConfigureAwait(false);
                }
                break;

            case PagePath:
                {
                    context.Response.ContentType = "text/html";
                    await context.Response.WriteAsync("<html><body>Censio</body></html>", context.RequestAborted).ConfigureAwait(false);
                }
                break;

            case RedirectPath:
                {
                    context.Response.StatusCode = StatusCodes.Status302Found;
                    context.Response.Headers.Location = new UriBuilder(HttpAddress)
                                                        {
                                                            Path = IngestPath
                                                        }.Uri.ToString();
                }
                break;

            case HeldPath:
                {
                    _heldRequest.TrySetResult();
                    await WaitUntilStoppedAsync(context.RequestAborted).ConfigureAwait(false);
                }
                break;

            default:
                {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                }
                break;
        }
    }

    /// <summary>
    /// Records a request to the WebSocket endpoint and accepts its upgrade, then reads until the client closes.
    /// </summary>
    /// <param name="context">The request's context</param>
    /// <returns>A task that completes when the connection ends</returns>
    private async Task HandleEndpointAsync(HttpContext context)
    {
        _endpointRequests.Enqueue(context.Request.Headers.ToDictionary(header => header.Key, header => header.Value.ToString(), StringComparer.OrdinalIgnoreCase));

        if (context.WebSockets.IsWebSocketRequest == false)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;

            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
        var buffer = new byte[1024];

        try
        {
            var result = await socket.ReceiveAsync(buffer, _stopping.Token).ConfigureAwait(false);

            while (result.MessageType != WebSocketMessageType.Close)
            {
                result = await socket.ReceiveAsync(buffer, _stopping.Token).ConfigureAwait(false);
            }

            await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, _stopping.Token).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is WebSocketException or OperationCanceledException)
        {
            socket.Abort();
        }
    }

    /// <summary>
    /// Waits until the host stops or the request is aborted.
    /// </summary>
    /// <param name="requestAborted">The token of the aborted request</param>
    /// <returns>A task that completes when the wait ends</returns>
    private async Task WaitUntilStoppedAsync(CancellationToken requestAborted)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(_stopping.Token, requestAborted);

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // The host stops or the client gave up.
        }
    }

    /// <summary>
    /// Accepts the proxy's connections, counts them and closes each at once.
    /// </summary>
    /// <returns>A task that completes when the host stops</returns>
    private async Task AcceptProxyConnectionsAsync()
    {
        try
        {
            while (_stopping.IsCancellationRequested == false)
            {
                using var client = await _proxy.AcceptTcpClientAsync(_stopping.Token).ConfigureAwait(false);

                Interlocked.Increment(ref _proxyConnections);
            }
        }
        catch (Exception exception) when (exception is OperationCanceledException or SocketException or ObjectDisposedException)
        {
            // The host stops.
        }
    }

    #endregion // Methods

    #region IAsyncDisposable

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync().ConfigureAwait(false);
        _proxy.Dispose();
        await _proxyLoop.ConfigureAwait(false);
        await _application.StopAsync(CancellationToken.None).ConfigureAwait(false);
        await _application.DisposeAsync().ConfigureAwait(false);
        _certificate.Dispose();
        _stopping.Dispose();
    }

    #endregion // IAsyncDisposable
}