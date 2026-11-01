using System.Text;

namespace Censio.Pipe.Tests;

/// <summary>
/// A write-only <see cref="Stream" /> that keeps the bytes written, holds writes until the test releases them, fails a
/// write or a flush after a given length with a given exception, counts writes and flushes, remembers how many bytes
/// each flush covered and records its disposal. It models the failures of a <see cref="FileStream" /> (a full disk as
/// <see cref="IOException" />, a deleted folder as <see cref="DirectoryNotFoundException" />, a refused access as
/// <see cref="UnauthorizedAccessException" />, checked by reading <c>dotnet/runtime</c> <c>release/10.0</c>
/// <c>Win32Marshal.GetExceptionForWin32Error</c>); a failed write keeps none of its bytes. Only the asynchronous
/// <c>WriteAsync</c> and <c>FlushAsync</c> are supported.
/// </summary>
public sealed class ControlledWriteStream : Stream
{
    #region Fields

    /// <summary>
    /// The bytes written so far.
    /// </summary>
    private readonly MemoryStream _content = new();

    /// <summary>
    /// The length of the content at each flush that succeeded.
    /// </summary>
    private readonly List<long> _flushedLengths = [];

    /// <summary>
    /// The tests waiting for a number of writes.
    /// </summary>
    private readonly List<(int Count, TaskCompletionSource Source)> _writeWaiters = [];

    /// <summary>
    /// The tests waiting for a flushed length.
    /// </summary>
    private readonly List<(long Length, TaskCompletionSource Source)> _flushWaiters = [];

    /// <summary>
    /// The lock of the state.
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>
    /// The completion that holds the writes, or <see langword="null" /> while they are not held.
    /// </summary>
    private TaskCompletionSource? _hold;

    /// <summary>
    /// The number of writes started so far.
    /// </summary>
    private int _writeCount;

    /// <summary>
    /// The number of flushes started so far.
    /// </summary>
    private int _flushCount;

    /// <summary>
    /// The length after which a write fails, or <see langword="null" />.
    /// </summary>
    private long? _writeFailureLength;

    /// <summary>
    /// The exception of the failing write.
    /// </summary>
    private Exception? _writeFailure;

    /// <summary>
    /// The length after which a flush fails, or <see langword="null" />.
    /// </summary>
    private long? _flushFailureLength;

    /// <summary>
    /// The exception of the failing flush.
    /// </summary>
    private Exception? _flushFailure;

    /// <summary>
    /// A value indicating whether the stream was disposed.
    /// </summary>
    private bool _disposed;

    #endregion // Fields

    #region Properties

    /// <summary>
    /// Gets the number of writes started so far, held ones included.
    /// </summary>
    public int WriteCount
    {
        get
        {
            lock (_lock)
            {
                return _writeCount;
            }
        }
    }

    /// <summary>
    /// Gets the number of flushes started so far, failed ones included.
    /// </summary>
    public int FlushCount
    {
        get
        {
            lock (_lock)
            {
                return _flushCount;
            }
        }
    }

    /// <summary>
    /// Gets the length of the content at each flush that succeeded.
    /// </summary>
    public IReadOnlyList<long> FlushedLengths
    {
        get
        {
            lock (_lock)
            {
                return [.. _flushedLengths];
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

    /// <summary>
    /// Gets the bytes written so far decoded as UTF-8.
    /// </summary>
    public string Text
    {
        get
        {
            lock (_lock)
            {
                return Encoding.UTF8.GetString(_content.ToArray());
            }
        }
    }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Holds every write that starts from now on until <see cref="ReleaseWrites" /> is called.
    /// </summary>
    public void HoldWrites()
    {
        lock (_lock)
        {
            _hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    /// <summary>
    /// Releases the held writes and stops holding.
    /// </summary>
    public void ReleaseWrites()
    {
        TaskCompletionSource? hold;

        lock (_lock)
        {
            hold = _hold;
            _hold = null;
        }

        hold?.TrySetResult();
    }

    /// <summary>
    /// Makes a write that would take the length of the content over the given length fail with the exception, keeping
    /// none of its bytes.
    /// </summary>
    /// <param name="length">The length that can still be written</param>
    /// <param name="exception">The exception</param>
    public void FailWritesAfter(long length, Exception exception)
    {
        lock (_lock)
        {
            _writeFailureLength = length;
            _writeFailure = exception;
        }
    }

    /// <summary>
    /// Makes a flush fail with the exception while the length of the content is over the given length.
    /// </summary>
    /// <param name="length">The length a flush can still cover</param>
    /// <param name="exception">The exception</param>
    public void FailFlushesAfter(long length, Exception exception)
    {
        lock (_lock)
        {
            _flushFailureLength = length;
            _flushFailure = exception;
        }
    }

    /// <summary>
    /// Gets a copy of the bytes written so far.
    /// </summary>
    /// <returns>The bytes</returns>
    public byte[] ToArray()
    {
        lock (_lock)
        {
            return _content.ToArray();
        }
    }

    /// <summary>
    /// Waits until the given number of writes were started.
    /// </summary>
    /// <param name="count">The number of writes</param>
    /// <param name="cancellationToken">The token that ends the wait</param>
    /// <returns>The task that completes when the writes were started</returns>
    public Task WaitForWritesAsync(int count, CancellationToken cancellationToken)
    {
        TaskCompletionSource source;

        lock (_lock)
        {
            source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            if (_writeCount >= count)
            {
                source.SetResult();
            }
            else
            {
                _writeWaiters.Add((count, source));
            }
        }

        return source.Task.WaitAsync(cancellationToken);
    }

    /// <summary>
    /// Waits until a flush covered at least the given number of bytes.
    /// </summary>
    /// <param name="length">The number of bytes</param>
    /// <param name="cancellationToken">The token that ends the wait</param>
    /// <returns>The task that completes when such a flush succeeded</returns>
    public Task WaitForFlushedBytesAsync(long length, CancellationToken cancellationToken)
    {
        TaskCompletionSource source;

        lock (_lock)
        {
            source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            if (_flushedLengths.Any(flushed => flushed >= length))
            {
                source.SetResult();
            }
            else
            {
                _flushWaiters.Add((length, source));
            }
        }

        return source.Task.WaitAsync(cancellationToken);
    }

    /// <summary>
    /// Starts a write: counts it, wakes the tests waiting for it and returns the hold it has to wait for.
    /// </summary>
    /// <returns>The hold, or <see langword="null" /> when the write is not held</returns>
    private TaskCompletionSource? BeginWrite()
    {
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            _writeCount++;

            foreach (var waiter in _writeWaiters.Where(waiter => _writeCount >= waiter.Count).ToList())
            {
                waiter.Source.TrySetResult();
                _writeWaiters.Remove(waiter);
            }

            return _hold;
        }
    }

    /// <summary>
    /// Appends the bytes of a write unless the write is set to fail.
    /// </summary>
    /// <param name="buffer">The bytes</param>
    private void Append(ReadOnlyMemory<byte> buffer)
    {
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_writeFailure is not null && _content.Length + buffer.Length > _writeFailureLength)
            {
                throw _writeFailure;
            }

            _content.Write(buffer.Span);
        }
    }

    #endregion // Methods

    #region Stream

    /// <inheritdoc />
    public override bool CanRead => false;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => _disposed == false;

    /// <inheritdoc />
    public override long Length
    {
        get
        {
            lock (_lock)
            {
                return _content.Length;
            }
        }
    }

    /// <inheritdoc />
    public override long Position
    {
        get => Length;
        set => throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override void Flush()
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return Task.FromException(new ObjectDisposedException(nameof(ControlledWriteStream)));
            }

            _flushCount++;

            if (_flushFailure is not null && _content.Length > _flushFailureLength)
            {
                return Task.FromException(_flushFailure);
            }

            _flushedLengths.Add(_content.Length);

            foreach (var waiter in _flushWaiters.Where(waiter => _content.Length >= waiter.Length).ToList())
            {
                waiter.Source.TrySetResult();
                _flushWaiters.Remove(waiter);
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
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
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    /// <inheritdoc />
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var hold = BeginWrite();

        if (hold is not null)
        {
            await hold.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        Append(buffer);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        lock (_lock)
        {
            _disposed = true;
        }

        base.Dispose(disposing);
    }

    #endregion // Stream
}