using Microsoft.Extensions.Logging;

namespace Censio.Companion.Core;

/// <summary>
/// The bounded buffer of live messages that wait for the server, kept in files.
/// </summary>
public sealed class LiveBuffer : IAsyncDisposable
{
    #region Constants

    /// <summary>
    /// The number of messages the memory-only list keeps.
    /// </summary>
    public const int MaxMemoryMessages = 64;

    /// <summary>
    /// The number of bytes the memory-only list and the pending list keep.
    /// </summary>
    public const int MaxMemoryBytes = 16777216;

    #endregion // Constants

    #region Fields

#pragma warning disable S4487
    // Skeleton: removed with the implementation
    /// <summary>
    /// The buffer folder.
    /// </summary>
    private readonly string _folder;
#pragma warning restore S4487

#pragma warning disable S4487
    // Skeleton: removed with the implementation
    /// <summary>
    /// The limits.
    /// </summary>
    private readonly LiveBufferLimits _limits;
#pragma warning restore S4487

#pragma warning disable S4487
    // Skeleton: removed with the implementation
    /// <summary>
    /// The time provider.
    /// </summary>
    private readonly TimeProvider _timeProvider;
#pragma warning restore S4487

#pragma warning disable S4487
    // Skeleton: removed with the implementation
    /// <summary>
    /// The logger.
    /// </summary>
    private readonly ILogger _logger;
#pragma warning restore S4487

    #endregion // Fields

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="LiveBuffer" /> class.
    /// </summary>
    /// <param name="folder">The buffer folder</param>
    /// <param name="limits">The limits</param>
    /// <param name="timeProvider">The time provider</param>
    /// <param name="logger">The logger</param>
    public LiveBuffer(string folder, LiveBufferLimits limits, TimeProvider timeProvider, ILogger logger)
    {
        _folder = folder;
        _limits = limits;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    #endregion // Constructor

    #region Properties

#pragma warning disable S2325, CA1822
    // Skeleton: removed with the implementation
    /// <summary>
    /// Gets the status.
    /// </summary>
    public LiveBufferStatus Status => default;
#pragma warning restore S2325, CA1822

    #endregion // Properties

    #region Methods

#pragma warning disable S2325, RH2003
    // Skeleton: removed with the implementation
    /// <summary>
    /// Adds a message.
    /// </summary>
    /// <param name="id">The identity of the message</param>
    /// <param name="utf8Json">The UTF-8 JSON of the message</param>
    public void Add(Guid id, ReadOnlyMemory<byte> utf8Json)
    {
        throw new NotImplementedException();
    }
#pragma warning restore S2325, RH2003

#pragma warning disable S2325, RH2003
    // Skeleton: removed with the implementation
    /// <summary>
    /// Loads the stored messages and writes the pending ones until cancelled.
    /// </summary>
    /// <param name="cancellationToken">The token that ends the run</param>
    /// <returns>The task of the run</returns>
    public Task RunAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
#pragma warning restore S2325, RH2003

#pragma warning disable S2325, RH2003
    // Skeleton: removed with the implementation
    /// <summary>
    /// Waits for the next message and marks it in flight.
    /// </summary>
    /// <param name="cancellationToken">The token that ends the wait</param>
    /// <returns>The next message</returns>
    public Task<LiveBufferEntry> NextAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
#pragma warning restore S2325, RH2003

#pragma warning disable S2325, RH2003
    // Skeleton: removed with the implementation
    /// <summary>
    /// Removes the message in flight after the server acknowledged it.
    /// </summary>
    /// <param name="id">The identity of the message</param>
    public void Acknowledge(Guid id)
    {
        throw new NotImplementedException();
    }
#pragma warning restore S2325, RH2003

#pragma warning disable S2325, RH2003
    // Skeleton: removed with the implementation
    /// <summary>
    /// Clears the in-flight mark after a failed connection.
    /// </summary>
    public void ReleaseInFlight()
    {
        throw new NotImplementedException();
    }
#pragma warning restore S2325, RH2003

#pragma warning disable S2325, RH2003
    // Skeleton: removed with the implementation
    /// <summary>
    /// Takes the number of dropped messages that were not logged yet.
    /// </summary>
    /// <returns>The number of dropped messages</returns>
    public int TakeUnloggedDropped()
    {
        throw new NotImplementedException();
    }
#pragma warning restore S2325, RH2003

    #endregion // Methods

    #region IAsyncDisposable

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    #endregion // IAsyncDisposable
}