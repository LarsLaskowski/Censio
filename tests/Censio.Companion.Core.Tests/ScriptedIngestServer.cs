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
        return new ContractAcknowledgement
               {
                   Id = id,
                   Refusal = refusal,
                   RefusedVersion = null,
                   MissingIcons = []
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
    public async Task<ContractMessage?> AcceptHelloAsync(CancellationToken cancellationToken)
    {
        var hello = await ReceiveAsync(cancellationToken).ConfigureAwait(false);

        await SendAsync(new ContractHelloAccepted
                        {
                            ServerVersion = ServerVersion,
                            CatalogIdentity = null
                        },
                        cancellationToken).ConfigureAwait(false);

        return hello;
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
        await _server.SendAsync(data, messageType, true, cancellationToken).ConfigureAwait(false);
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

        return ValueTask.CompletedTask;
    }

    #endregion // IAsyncDisposable
}