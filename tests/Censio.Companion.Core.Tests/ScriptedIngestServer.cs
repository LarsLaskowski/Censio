using System.Buffers;
using System.Net.WebSockets;

using Censio.Contracts;
using Censio.Server.Tests;

namespace Censio.Companion.Core.Tests;

/// <summary>
/// The server side of an in-memory WebSocket connection: reads the Companion's messages with <see cref="ContractReader" />
/// and answers as the test scripts it.
/// </summary>
public sealed class ScriptedIngestServer : IAsyncDisposable
{
    #region Constants

    /// <summary>
    /// The server version of an accepted hello.
    /// </summary>
    public const string ServerVersion = "9.8.7";

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The server's end of the connection.
    /// </summary>
    private readonly InMemoryDuplexStream _serverStream;

    /// <summary>
    /// The client's end of the connection.
    /// </summary>
    private readonly InMemoryDuplexStream _clientStream;

    /// <summary>
    /// The server's socket.
    /// </summary>
    private readonly WebSocket _server;

    /// <summary>
    /// The lock that lets one message at a time be written to the server's socket, as the test and its serving loop may both
    /// acknowledge.
    /// </summary>
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    /// <summary>
    /// Whether the client sent a close frame.
    /// </summary>
    private volatile bool _isClosedByClient;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ScriptedIngestServer" /> class.
    /// </summary>
    /// <param name="serverStream">The server's end of the connection</param>
    /// <param name="clientStream">The client's end of the connection</param>
    private ScriptedIngestServer(InMemoryDuplexStream serverStream, InMemoryDuplexStream clientStream)
    {
        _serverStream = serverStream;
        _clientStream = clientStream;
        _server = WebSocket.CreateFromStream(serverStream,
                                             new WebSocketCreationOptions
                                             {
                                                 IsServer = true,
                                                 KeepAliveInterval = TimeSpan.Zero
                                             });
        ClientSocket = WebSocket.CreateFromStream(clientStream,
                                                  new WebSocketCreationOptions
                                                  {
                                                      IsServer = false,
                                                      KeepAliveInterval = TimeSpan.Zero
                                                  });
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the client's socket, which a connector hands to the sender.
    /// </summary>
    public WebSocket ClientSocket { get; }

    /// <summary>
    /// Gets the number of bytes the client has read from the connection.
    /// </summary>
    public long ClientBytesRead => _clientStream.BytesRead;

    /// <summary>
    /// Gets a value indicating whether the client sent a close frame, rather than ending the connection without one.
    /// </summary>
    public bool IsClosedByClient => _isClosedByClient;

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Creates a server with a new connection.
    /// </summary>
    /// <returns>The server</returns>
    public static ScriptedIngestServer Create()
    {
        var (serverStream, clientStream) = InMemoryDuplexStream.CreatePair();

        return new ScriptedIngestServer(serverStream, clientStream);
    }

    /// <summary>
    /// Returns the identity of a message that is acknowledged.
    /// </summary>
    /// <param name="message">The message</param>
    /// <returns>The identity, or <see cref="Guid.Empty" /> for a message without one</returns>
    public static Guid IdOf(ContractMessage? message)
    {
        return message switch
               {
                   ContractTick tick => tick.Id,
                   ContractSessionStart start => start.Id,
                   ContractSessionEnd end => end.Id,
                   ContractTradeRoutes routes => routes.Id,
                   ContractCatalogUpload upload => upload.Id,
                   ContractIconImage icon => icon.Id,
                   ContractProfileDeletion deletion => deletion.Id,
                   ContractProfileMerge merge => merge.Id,
                   _ => Guid.Empty
               };
    }

    /// <summary>
    /// Creates an acknowledgement.
    /// </summary>
    /// <param name="id">The identity of the acknowledged message</param>
    /// <param name="refusal">The refusal, or <c>null</c> for an accepted message</param>
    /// <returns>The acknowledgement</returns>
    public static ContractAcknowledgement CreateAcknowledgement(Guid id, ContractAcknowledgementRefusal? refusal)
    {
        return CreateAcknowledgement(id, refusal, []);
    }

    /// <summary>
    /// Creates an acknowledgement that lists the icons the server still misses.
    /// </summary>
    /// <param name="id">The identity of the acknowledged message</param>
    /// <param name="refusal">The refusal, or <c>null</c> for an accepted message</param>
    /// <param name="missingIcons">The references of the missing icons, in the order the server lists them</param>
    /// <returns>The acknowledgement</returns>
    public static ContractAcknowledgement CreateAcknowledgement(Guid id, ContractAcknowledgementRefusal? refusal, IReadOnlyList<string> missingIcons)
    {
        return new ContractAcknowledgement
               {
                   Id = id,
                   Refusal = refusal,
                   RefusedVersion = null,
                   MissingIcons = missingIcons
               };
    }

    /// <summary>
    /// Writes a message as the contract writer does.
    /// </summary>
    /// <param name="message">The message</param>
    /// <returns>The UTF-8 JSON of the message</returns>
    public static byte[] Write(ContractMessage message)
    {
        return ContractWriter.Write(message).Utf8Json.ToArray();
    }

    /// <summary>
    /// Receives the next message of the client. A close frame is answered with a close frame.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The message, or <c>null</c> when the client closed or the connection ended, also before this call</returns>
    public async Task<ContractMessage?> ReceiveAsync(CancellationToken cancellationToken)
    {
        var buffer = new ArrayBufferWriter<byte>();

        if (_server.State is not (WebSocketState.Open or WebSocketState.CloseSent))
        {
            return null;
        }

        try
        {
            var isComplete = false;

            while (isComplete == false)
            {
                var result = await _server.ReceiveAsync(buffer.GetMemory(4096), cancellationToken).ConfigureAwait(false);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _isClosedByClient = true;
                }

                if (result.MessageType == WebSocketMessageType.Close && _server.State == WebSocketState.CloseReceived)
                {
                    await _server.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, cancellationToken).ConfigureAwait(false);

                    return null;
                }

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return null;
                }

                buffer.Advance(result.Count);
                isComplete = result.EndOfMessage;
            }
        }
        catch (WebSocketException)
        {
            return null;
        }

        return ContractReader.Read(buffer.WrittenSpan).Message;
    }

    /// <summary>
    /// Receives the hello and accepts it.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The message received first, which should be the hello</returns>
    public Task<ContractMessage?> AcceptHelloAsync(CancellationToken cancellationToken)
    {
        return AcceptHelloAsync(null, cancellationToken);
    }

    /// <summary>
    /// Receives the hello and accepts it, naming the identity of the catalog the server holds.
    /// </summary>
    /// <param name="catalogIdentity">The identity the reply names, or <c>null</c> when the server holds no complete catalog</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The message received first, which should be the hello</returns>
    public async Task<ContractMessage?> AcceptHelloAsync(string? catalogIdentity, CancellationToken cancellationToken)
    {
        var hello = await ReceiveAsync(cancellationToken).ConfigureAwait(false);

        await SendAsync(new ContractHelloAccepted
                        {
                            ServerVersion = ServerVersion,
                            CatalogIdentity = catalogIdentity
                        },
                        cancellationToken).ConfigureAwait(false);

        return hello;
    }

    /// <summary>
    /// Accepts the hello and acknowledges every message after it, a status excepted, until the connection ends.
    /// </summary>
    /// <param name="isAbortingAtFirstTick">
    /// Whether the connection is ended without a close frame instead of acknowledging
    /// the first tick
    /// </param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The messages received, the hello first, in order</returns>
    public async Task<List<ContractMessage>> AcknowledgeAllAsync(bool isAbortingAtFirstTick, CancellationToken cancellationToken)
    {
        var messages = new List<ContractMessage>();
        var hello = await AcceptHelloAsync(cancellationToken).ConfigureAwait(false);

        if (hello is null)
        {
            return messages;
        }

        messages.Add(hello);

        var message = await ReceiveAsync(cancellationToken).ConfigureAwait(false);

        while (message is not null)
        {
            messages.Add(message);

            if (isAbortingAtFirstTick && message is ContractTick)
            {
                Abort();

                return messages;
            }

            if (message is not ContractStatus && await TryAcknowledgeAsync(message, cancellationToken).ConfigureAwait(false) == false)
            {
                return messages;
            }

            message = await ReceiveAsync(cancellationToken).ConfigureAwait(false);
        }

        return messages;
    }

    /// <summary>
    /// Acknowledges a message.
    /// </summary>
    /// <param name="message">The message</param>
    /// <param name="refusal">The refusal, or <c>null</c> for an accepted message</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A task that completes when the acknowledgement is sent</returns>
    public Task AcknowledgeAsync(ContractMessage? message, ContractAcknowledgementRefusal? refusal, CancellationToken cancellationToken)
    {
        return SendAsync(CreateAcknowledgement(IdOf(message), refusal), cancellationToken);
    }

    /// <summary>
    /// Acknowledges a message and lists the icons the server still misses.
    /// </summary>
    /// <param name="message">The message</param>
    /// <param name="refusal">The refusal, or <c>null</c> for an accepted message</param>
    /// <param name="missingIcons">The references of the missing icons, in the order the server lists them</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A task that completes when the acknowledgement is sent</returns>
    public Task AcknowledgeAsync(ContractMessage? message, ContractAcknowledgementRefusal? refusal, IReadOnlyList<string> missingIcons, CancellationToken cancellationToken)
    {
        return SendAsync(CreateAcknowledgement(IdOf(message), refusal, missingIcons), cancellationToken);
    }

    /// <summary>
    /// Sends a message as the contract writer writes it.
    /// </summary>
    /// <param name="message">The message</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A task that completes when the message is sent</returns>
    public Task SendAsync(ContractMessage message, CancellationToken cancellationToken)
    {
        return SendRawAsync(Write(message), WebSocketMessageType.Text, cancellationToken);
    }

    /// <summary>
    /// Sends bytes as one message of one frame.
    /// </summary>
    /// <param name="data">The bytes</param>
    /// <param name="messageType">The type of the message</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A task that completes when the message is sent</returns>
    public async Task SendRawAsync(byte[] data, WebSocketMessageType messageType, CancellationToken cancellationToken)
    {
        await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await _server.SendAsync(data, messageType, true, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <summary>
    /// Sends bytes as one message that the client may stop reading, ignoring the failure of the ended connection.
    /// </summary>
    /// <param name="data">The bytes</param>
    /// <param name="messageType">The type of the message</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A task that completes when the message is sent or the connection ended</returns>
    public async Task SendRawIgnoringFailureAsync(byte[] data, WebSocketMessageType messageType, CancellationToken cancellationToken)
    {
        try
        {
            await SendRawAsync(data, messageType, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is WebSocketException or IOException or OperationCanceledException)
        {
            // The client ended the connection while the message was sent.
        }
    }

    /// <summary>
    /// Sends a close frame without waiting for the client's.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A task that completes when the close frame is sent</returns>
    public Task CloseOutputAsync(CancellationToken cancellationToken)
    {
        return _server.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, cancellationToken);
    }

    /// <summary>
    /// Ends the connection without a close frame, as a server that stops or a network that fails.
    /// </summary>
    public void Abort()
    {
        _server.Abort();
        _serverStream.Dispose();
    }

    /// <summary>
    /// Acknowledges a message as accepted, unless the connection ended.
    /// </summary>
    /// <param name="message">The message</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns><c>true</c> if the acknowledgement was sent</returns>
    private async Task<bool> TryAcknowledgeAsync(ContractMessage message, CancellationToken cancellationToken)
    {
        try
        {
            await AcknowledgeAsync(message, null, cancellationToken).ConfigureAwait(false);

            return true;
        }
        catch (Exception exception) when (exception is WebSocketException or IOException)
        {
            return false;
        }
    }

    #endregion // Methods

    #region IAsyncDisposable

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _server.Abort();
        ClientSocket.Abort();
        _server.Dispose();
        ClientSocket.Dispose();
        _serverStream.Dispose();
        _clientStream.Dispose();
        _sendLock.Dispose();

        return ValueTask.CompletedTask;
    }

    #endregion // IAsyncDisposable
}