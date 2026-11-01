namespace Censio.Companion.Core.Tests;

/// <summary>
/// A read-only <see cref="Stream" /> the test feeds with bytes, ends or fails, standing in for an asynchronous pipe
/// stream: a read returns the bytes fed so far, waits while there are none, returns 0 once the stream was ended (a
/// broken or closed pipe) and throws the given exception once it failed; a pending read ends with an
/// <see cref="OperationCanceledException" /> when its token is cancelled. Feeding, ending or failing completes a
/// pending read on the thread of the test, so the code waiting for it continues there. It counts the reads that had
/// to wait and every write call, and records its disposal.
/// </summary>
public sealed class ControlledPipeStream : Stream
{
    #region Fields

    /// <summary>
    /// The bytes fed and not yet read.
    /// </summary>
    private readonly List<byte> _unread = [];

    /// <summary>
    /// The tests waiting for a number of pending reads.
    /// </summary>
    private readonly List<(int Count, TaskCompletionSource Source)> _waiters = [];

    /// <summary>
    /// The lock of the buffer, the pending read and the counters.
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>
    /// The completion of the read that waits for bytes, if any.
    /// </summary>
    private TaskCompletionSource<int>? _pending;

    /// <summary>
    /// The buffer of the read that waits for bytes.
    /// </summary>
    private Memory<byte> _pendingBuffer;

    /// <summary>
    /// The cancellation registration of the read that waits for bytes.
    /// </summary>
    private CancellationTokenRegistration _pendingRegistration;

    /// <summary>
    /// A value indicating whether the stream was ended.
    /// </summary>
    private bool _ended;

    /// <summary>
    /// The exception every read throws once the bytes are read, if the stream failed.
    /// </summary>
    private Exception? _failure;

    /// <summary>
    /// The number of reads that had to wait for bytes.
    /// </summary>
    private int _pendingReadCount;

    /// <summary>
    /// The number of write calls.
    /// </summary>
    private int _writeCallCount;

    /// <summary>
    /// A value indicating whether the stream was disposed.
    /// </summary>
    private bool _disposed;

    #endregion // Fields

    #region Properties

    /// <summary>
    /// Gets the number of reads that found no bytes and had to wait.
    /// </summary>
    public int PendingReadCount
    {
        get
        {
            lock (_lock)
            {
                return _pendingReadCount;
            }
        }
    }

    /// <summary>
    /// Gets the number of bytes fed and not yet read.
    /// </summary>
    public int UnreadByteCount
    {
        get
        {
            lock (_lock)
            {
                return _unread.Count;
            }
        }
    }

    /// <summary>
    /// Gets the number of write calls of any kind.
    /// </summary>
    public int WriteCallCount
    {
        get
        {
            lock (_lock)
            {
                return _writeCallCount;
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the stream was disposed.
    /// </summary>
    public bool IsDisposed
    {
        get
        {
            lock (_lock)
            {
                return _disposed;
            }
        }
    }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Appends bytes and completes a pending read with as many of them as fit.
    /// </summary>
    /// <param name="bytes">The bytes</param>
    public void Feed(byte[] bytes)
    {
        TaskCompletionSource<int>? completion;
        var count = 0;

        lock (_lock)
        {
            _unread.AddRange(bytes);

            completion = TakePending();

            if (completion is not null)
            {
                count = CopyUnread(_pendingBuffer.Span);
            }
        }

        completion?.TrySetResult(count);
    }

    /// <summary>
    /// Ends the stream: once the bytes are read, every read returns 0.
    /// </summary>
    public void End()
    {
        TaskCompletionSource<int>? completion;

        lock (_lock)
        {
            _ended = true;
            completion = TakePending();
        }

        completion?.TrySetResult(0);
    }

    /// <summary>
    /// Fails the stream: once the bytes are read, every read throws the exception.
    /// </summary>
    /// <param name="exception">The exception</param>
    public void Fail(Exception exception)
    {
        TaskCompletionSource<int>? completion;

        lock (_lock)
        {
            _failure = exception;
            completion = TakePending();
        }

        completion?.TrySetException(exception);
    }

    /// <summary>
    /// Waits until the given number of reads had to wait for bytes.
    /// </summary>
    /// <param name="count">The number of pending reads</param>
    /// <param name="cancellationToken">The token that ends the wait</param>
    /// <returns>The task that completes when the reads were pending</returns>
    public Task WaitForPendingReadsAsync(int count, CancellationToken cancellationToken)
    {
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (_lock)
        {
            if (_pendingReadCount >= count)
            {
                source.SetResult();
            }
            else
            {
                _waiters.Add((count, source));
            }
        }

        return source.Task.WaitAsync(cancellationToken);
    }

    /// <summary>
    /// Takes the pending read and its registration away; called under the lock.
    /// </summary>
    /// <returns>The completion of the pending read, or <c>null</c> if no read waits</returns>
    private TaskCompletionSource<int>? TakePending()
    {
        var completion = _pending;

        _pending = null;
        _pendingRegistration.Unregister();
        _pendingRegistration = default;

        return completion;
    }

    /// <summary>
    /// Moves unread bytes into a buffer; called under the lock.
    /// </summary>
    /// <param name="buffer">The buffer</param>
    /// <returns>The number of bytes moved</returns>
    private int CopyUnread(Span<byte> buffer)
    {
        var count = Math.Min(buffer.Length, _unread.Count);

        for (var index = 0; index < count; index++)
        {
            buffer[index] = _unread[index];
        }

        _unread.RemoveRange(0, count);

        return count;
    }

    /// <summary>
    /// Counts a write call and rejects it.
    /// </summary>
    /// <returns>The exception to throw</returns>
    private NotSupportedException RecordWrite()
    {
        lock (_lock)
        {
            _writeCallCount++;
        }

        return new NotSupportedException("The pipe is read-only.");
    }

    /// <summary>
    /// Cancels a pending read.
    /// </summary>
    /// <param name="completion">The completion of the read</param>
    /// <param name="cancellationToken">The cancelled token</param>
    private void CancelPending(TaskCompletionSource<int> completion, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (ReferenceEquals(_pending, completion))
            {
                _pending = null;
            }
        }

        completion.TrySetCanceled(cancellationToken);
    }

    #endregion // Methods

    #region Stream

    /// <inheritdoc />
    public override bool CanRead => IsDisposed == false;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override void Flush()
    {
        // A read-only stream has nothing to flush
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {
        return ReadAsync(buffer.AsMemory(offset, count), CancellationToken.None).AsTask().GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    /// <inheritdoc />
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        TaskCompletionSource<int> completion;
        List<TaskCompletionSource> reached;

        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (cancellationToken.IsCancellationRequested)
            {
                return ValueTask.FromCanceled<int>(cancellationToken);
            }

            if (_unread.Count > 0 || buffer.IsEmpty)
            {
                return ValueTask.FromResult(CopyUnread(buffer.Span));
            }

            if (_failure is not null)
            {
                return ValueTask.FromException<int>(_failure);
            }

            if (_ended)
            {
                return ValueTask.FromResult(0);
            }

            completion = new TaskCompletionSource<int>();
            _pending = completion;
            _pendingBuffer = buffer;
            _pendingReadCount++;

            reached = [.. _waiters.Where(waiter => _pendingReadCount >= waiter.Count).Select(waiter => waiter.Source)];
            _waiters.RemoveAll(waiter => _pendingReadCount >= waiter.Count);

            _pendingRegistration = cancellationToken.Register(() => CancelPending(completion, cancellationToken));
        }

        reached.ForEach(source => source.TrySetResult());

        return new ValueTask<int>(completion.Task);
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count)
    {
        throw RecordWrite();
    }

    /// <inheritdoc />
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        throw RecordWrite();
    }

    /// <inheritdoc />
    public override void WriteByte(byte value)
    {
        throw RecordWrite();
    }

    /// <inheritdoc />
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return Task.FromException(RecordWrite());
    }

    /// <inheritdoc />
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromException(RecordWrite());
    }

    /// <inheritdoc />
    public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
    {
        throw RecordWrite();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        TaskCompletionSource<int>? completion;

        lock (_lock)
        {
            _disposed = true;
            completion = TakePending();
        }

        completion?.TrySetException(new ObjectDisposedException(nameof(ControlledPipeStream)));

        base.Dispose(disposing);
    }

    #endregion // Stream
}