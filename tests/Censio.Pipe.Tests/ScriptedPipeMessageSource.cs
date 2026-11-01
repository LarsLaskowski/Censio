namespace Censio.Pipe.Tests;

/// <summary>
/// An <see cref="IPipeMessageSource" /> that returns queued results, either completed at once or held until the test
/// releases them, counts its reads and records whether a token it received was cancelled. It models no real component.
/// </summary>
public sealed class ScriptedPipeMessageSource : IPipeMessageSource
{
    #region Fields

    /// <summary>
    /// The reads that are still to be handed out.
    /// </summary>
    private readonly Queue<(TaskCompletionSource<PipeReadResult> Completion, Action? OnRead)> _script = [];

    /// <summary>
    /// The tests waiting for a number of reads.
    /// </summary>
    private readonly List<(int Count, TaskCompletionSource Source)> _waiters = [];

    /// <summary>
    /// The lock of the counters and the waiters.
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>
    /// The number of reads requested so far.
    /// </summary>
    private int _readCount;

    /// <summary>
    /// A value indicating whether the token of a pending read was cancelled.
    /// </summary>
    private bool _tokenCancelled;

    #endregion // Fields

    #region Properties

    /// <summary>
    /// Gets the number of reads requested so far.
    /// </summary>
    public int ReadCount
    {
        get
        {
            lock (_lock)
            {
                return _readCount;
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the token of a read that had not completed was cancelled.
    /// </summary>
    public bool TokenCancelled
    {
        get
        {
            lock (_lock)
            {
                return _tokenCancelled;
            }
        }
    }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Queues a read that completes at once.
    /// </summary>
    /// <param name="result">The result of the read</param>
    /// <param name="onRead">An action run when the read is requested, before it returns; for example to set a clock</param>
    public void Enqueue(PipeReadResult result, Action? onRead = null)
    {
        var completion = new TaskCompletionSource<PipeReadResult>();

        completion.SetResult(result);

        _script.Enqueue((completion, onRead));
    }

    /// <summary>
    /// Queues a read that stays pending until it is released.
    /// </summary>
    /// <returns>The handle that releases the read</returns>
    public HeldPipeRead EnqueueHeld()
    {
        var completion = new TaskCompletionSource<PipeReadResult>();

        _script.Enqueue((completion, null));

        return new HeldPipeRead(completion);
    }

    /// <summary>
    /// Waits until the given number of reads were requested.
    /// </summary>
    /// <param name="count">The number of reads</param>
    /// <param name="cancellationToken">The token that ends the wait</param>
    /// <returns>The task that completes when the reads were requested</returns>
    public Task WaitForReadsAsync(int count, CancellationToken cancellationToken)
    {
        TaskCompletionSource source;

        lock (_lock)
        {
            source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            if (_readCount >= count)
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

    #endregion // Methods

    #region IPipeMessageSource

    /// <inheritdoc />
    public ValueTask<PipeReadResult> ReadAsync(CancellationToken cancellationToken)
    {
        (TaskCompletionSource<PipeReadResult> Completion, Action? OnRead) read;

        lock (_lock)
        {
            _readCount++;

            foreach (var waiter in _waiters.Where(waiter => _readCount >= waiter.Count).ToList())
            {
                waiter.Source.TrySetResult();
                _waiters.Remove(waiter);
            }

            read = _script.Count > 0 ? _script.Dequeue() : (new TaskCompletionSource<PipeReadResult>(), null);
        }

        read.OnRead?.Invoke();

        if (read.Completion.Task.IsCompleted)
        {
            return new ValueTask<PipeReadResult>(read.Completion.Task);
        }

        cancellationToken.Register(() =>
                                   {
                                       lock (_lock)
                                       {
                                           _tokenCancelled = true;
                                       }

                                       read.Completion.TrySetCanceled(cancellationToken);
                                   });

        return new ValueTask<PipeReadResult>(read.Completion.Task);
    }

    #endregion // IPipeMessageSource
}