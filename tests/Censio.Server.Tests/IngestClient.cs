using System.Net.WebSockets;
using System.Text;

using Censio.Contracts;

namespace Censio.Server.Tests;

/// <summary>
/// The Companion's side of an ingest connection in the tests: the messages it sends in single-quote notation and the
/// sends and receives over a WebSocket, each bounded by a real-time guard so a broken server cannot hang a test.
/// </summary>
public static class IngestClient
{
    #region Constants

    /// <summary>
    /// A valid profile identity.
    /// </summary>
    public const string Profile = "aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee";

    /// <summary>
    /// A valid receive time.
    /// </summary>
    public const string Time = "2026-11-12T10:20:30.1234567Z";

    /// <summary>
    /// The PNG data of the icon image, in Base64.
    /// </summary>
    public const string PngBase64 = "iVBORw==";

    /// <summary>
    /// The SHA-256 reference of <see cref="PngBase64" />.
    /// </summary>
    public const string PngReference = "0f4636c78f65d3639ece5a064b5ae753e3408614a14fb18ab4d7540d2c248543";

    /// <summary>
    /// The size of the buffer a reply is received in.
    /// </summary>
    private const int ReceiveBufferSize = 65536;

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The real-time limit of every send and receive.
    /// </summary>
    private static readonly TimeSpan _guard = TimeSpan.FromSeconds(10);

    #endregion // Fields

    #region Methods

    /// <summary>
    /// Builds a hello.
    /// </summary>
    /// <param name="token">The token, or <c>null</c> for <c>token</c> <c>null</c></param>
    /// <param name="version">The contract version</param>
    /// <returns>The hello in single-quote notation</returns>
    public static string Hello(string? token, int version = 1)
    {
        var tokenValue = token is null ? "null" : $"'{token}'";

        return $"{{'type':'hello','contractVersion':{version},'token':{tokenValue},'companionVersion':'MARKERVERSION','profileId':'{Profile}','profileName':'MARKERPROFILE'}}";
    }

    /// <summary>
    /// Builds a tick.
    /// </summary>
    /// <param name="id">The identity of the message</param>
    /// <returns>The tick in single-quote notation</returns>
    public static string Tick(string id)
    {
        return $"{{'type':'tick','contractVersion':1,'id':'{id}','profileId':'{Profile}','receiveTime':'{Time}','gameTime':42,'islands':[{{'sessionGuid':180023,'islandId':1,'areaIndex':0,'sessionId':2,'name':'MARKERISLAND','entries':[]}}]}}";
    }

    /// <summary>
    /// Builds a session start.
    /// </summary>
    /// <param name="id">The identity of the message</param>
    /// <returns>The session start in single-quote notation</returns>
    public static string SessionStart(string id)
    {
        return $"{{'type':'sessionStart','contractVersion':1,'id':'{id}','profileId':'{Profile}','receiveTime':'{Time}'}}";
    }

    /// <summary>
    /// Builds a session end.
    /// </summary>
    /// <param name="id">The identity of the message</param>
    /// <returns>The session end in single-quote notation</returns>
    public static string SessionEnd(string id)
    {
        return $"{{'type':'sessionEnd','contractVersion':1,'id':'{id}','profileId':'{Profile}','receiveTime':'{Time}'}}";
    }

    /// <summary>
    /// Builds an icon image.
    /// </summary>
    /// <param name="id">The identity of the message</param>
    /// <returns>The icon image in single-quote notation</returns>
    public static string IconImage(string id)
    {
        return $"{{'type':'iconImage','contractVersion':1,'id':'{id}','reference':'{PngReference}','png':'{PngBase64}'}}";
    }

    /// <summary>
    /// Builds a catalog upload around a catalog document.
    /// </summary>
    /// <param name="id">The identity of the message</param>
    /// <param name="document">The bytes of the catalog document</param>
    /// <returns>The catalog upload in single-quote notation</returns>
    public static string CatalogUpload(string id, byte[] document)
    {
        return $"{{'type':'catalogUpload','contractVersion':1,'id':'{id}','document':'{Convert.ToBase64String(document)}'}}";
    }

    /// <summary>
    /// Builds a status message.
    /// </summary>
    /// <returns>The status message in single-quote notation</returns>
    public static string Status()
    {
        return "{'type':'status','contractVersion':1,'pipeState':'receiving'}";
    }

    /// <summary>
    /// Turns the single-quote notation into JSON.
    /// </summary>
    /// <param name="notation">The text with single quotes</param>
    /// <returns>The text with double quotes</returns>
    public static string Json(string notation)
    {
        return notation.Replace('\'', '"');
    }

    /// <summary>
    /// Sends a message in single-quote notation as one text message.
    /// </summary>
    /// <param name="socket">The client socket</param>
    /// <param name="notation">The message in single-quote notation</param>
    /// <returns>The task</returns>
    public static Task SendAsync(WebSocket socket, string notation)
    {
        return SendPartAsync(socket, notation, true);
    }

    /// <summary>
    /// Sends a part of a text message.
    /// </summary>
    /// <param name="socket">The client socket</param>
    /// <param name="notation">The part in single-quote notation</param>
    /// <param name="endOfMessage">Whether the part ends the message</param>
    /// <returns>The task</returns>
    public static Task SendPartAsync(WebSocket socket, string notation, bool endOfMessage)
    {
        return socket.SendAsync(Encoding.UTF8.GetBytes(Json(notation)), WebSocketMessageType.Text, endOfMessage, CancellationToken.None).WaitAsync(_guard);
    }

    /// <summary>
    /// Sends parts of a text message of spaces, none of them ending it.
    /// </summary>
    /// <param name="socket">The client socket</param>
    /// <param name="partSize">The size of each part in bytes</param>
    /// <param name="parts">The number of parts</param>
    /// <returns>The task</returns>
    public static async Task SendSpacesAsync(WebSocket socket, int partSize, int parts)
    {
        var part = new byte[partSize];

        part.AsSpan().Fill((byte)' ');

        for (var index = 0; index < parts; index++)
        {
            await socket.SendAsync(part, WebSocketMessageType.Text, false, CancellationToken.None).WaitAsync(_guard).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Receives the next message or the close of the server.
    /// </summary>
    /// <param name="socket">The client socket</param>
    /// <returns>The message read with the contract reader, or the close status and description</returns>
    public static async Task<(ContractMessage? Message, WebSocketCloseStatus? CloseStatus, string? CloseDescription)> ReceiveAsync(WebSocket socket)
    {
        var buffer = new byte[ReceiveBufferSize];
        var length = 0;
        WebSocketReceiveResult result;

        do
        {
            result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer, length, buffer.Length - length), CancellationToken.None).WaitAsync(_guard).ConfigureAwait(false);
            length += result.Count;
        }
        while (result.EndOfMessage == false && result.MessageType != WebSocketMessageType.Close);

        if (result.MessageType == WebSocketMessageType.Close)
        {
            return (null, result.CloseStatus, result.CloseStatusDescription);
        }

        var read = ContractReader.Read(buffer.AsSpan(0, length));

        return (read.Message ?? throw new InvalidOperationException("The server sent a message the contract reader refuses: " + read.Error?.Text), null, null);
    }

    /// <summary>
    /// Receives every message until the server's close.
    /// </summary>
    /// <param name="socket">The client socket</param>
    /// <returns>The messages before the close, and the close status and description</returns>
    public static async Task<(IReadOnlyList<ContractMessage> Messages, WebSocketCloseStatus? CloseStatus, string? CloseDescription)> ReceiveUntilCloseAsync(WebSocket socket)
    {
        var messages = new List<ContractMessage>();

        while (true)
        {
            var (message, closeStatus, closeDescription) = await ReceiveAsync(socket).ConfigureAwait(false);

            if (message is null)
            {
                return (messages, closeStatus, closeDescription);
            }

            messages.Add(message);
        }
    }

    /// <summary>
    /// Answers the server's close, or closes the connection from the client side, without waiting for a reply.
    /// </summary>
    /// <param name="socket">The client socket</param>
    /// <returns>The task</returns>
    public static Task CloseAsync(WebSocket socket)
    {
        return socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None).WaitAsync(_guard);
    }

    /// <summary>
    /// Waits for a task with the real-time guard.
    /// </summary>
    /// <param name="task">The task</param>
    /// <returns>The task that completes with the given task, or fails after the guard</returns>
    public static Task GuardAsync(Task task)
    {
        return task.WaitAsync(_guard);
    }

    #endregion // Methods
}