namespace Censio.Companion.Core.Tests;

/// <summary>
/// An <see cref="IPipeConnector" /> that returns queued results, either completed at once or held until the test
/// releases them, counts its attempts and stamps each with the time of the given clock; an attempt without a script
/// entry stays pending until it is cancelled. A queued or held result completes on the thread of the test, so the
/// code waiting for it continues there. It models no real component.
/// </summary>
public sealed class ScriptedPipeConnector : IPipeConnector
{
    #region Fields

    /// <summary>
    /// The clock that stamps the attempts.
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// The attempts that are still to be handed out.
    /// </summary>
    private readonly Queue<TaskCompletionSource<PipeConnectResult>> _script = [];

    /// <summary>
    /// The times of the attempts made so far.
    /// </summary>
    private readonly List<DateTimeOffset> _attemptTimes = [];

    /// <summary>
    /// The tests waiting for a number of attempts.
    /// </summary>
    private readonly List<(int Count, TaskCompletionSource Source)> _waiters = [];

    /// <summary>
    /// The lock of the script, the attempts and the waiters.
    /// </summary>
    private readonly Lock _lock = new();

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ScriptedPipeConnector" /> class.
    /// </summary>
    /// <param name="timeProvider">The clock that stamps the attempts</param>
    public ScriptedPipeConnector(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the number of attempts made so far.
    /// </summary>
    public int AttemptCount
    {
        get
        {
            lock (_lock)
            {
                return _attemptTimes.Count;
            }
        }
    }

    /// <summary>
    /// Gets the times of the attempts made so far, in order.
    /// </summary>
    public IReadOnlyList<DateTimeOffset> AttemptTimes
    {
        get
        {
            lock (_lock)
            {
                return [.. _attemptTimes];
            }
        }
    }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Queues an attempt that completes at once with the given result.
    /// </summary>
    /// <param name="result">The result of the attempt</param>
    public void Enqueue(PipeConnectResult result)
    {
        var completion = new TaskCompletionSource<PipeConnectResult>();

        completion.SetResult(result);

        lock (_lock)
        {
            _script.Enqueue(completion);
        }
    }

    /// <summary>
    /// Queues an attempt that fails at once with the given exception, against the contract of the interface.
    /// </summary>
    /// <param name="exception">The exception of the attempt</param>
    public void EnqueueException(Exception exception)
    {
        var completion = new TaskCompletionSource<PipeConnectResult>();

        completion.SetException(exception);

        lock (_lock)
        {
            _script.Enqueue(completion);
        }
    }

    /// <summary>
    /// Queues an attempt that stays pending until it is released or cancelled.
    /// </summary>
    /// <returns>The action that releases the attempt with its result; the waiting code continues on the calling thread</returns>
    public Action<PipeConnectResult> EnqueueHeld()
    {
        var completion = new TaskCompletionSource<PipeConnectResult>();

        lock (_lock)
        {
            _script.Enqueue(completion);
        }

        return result => completion.TrySetResult(result);
    }

    /// <summary>
    /// Waits until the given number of attempts were made.
    /// </summary>
    /// <param name="count">The number of attempts</param>
    /// <param name="cancellationToken">The token that ends the wait</param>
    /// <returns>The task that completes when the attempts were made</returns>
    public Task WaitForAttemptsAsync(int count, CancellationToken cancellationToken)
    {
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (_lock)
        {
            if (_attemptTimes.Count >= count)
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

    #region IPipeConnector

    /// <inheritdoc />
    public ValueTask<PipeConnectResult> ConnectAsync(CancellationToken cancellationToken)
    {
        TaskCompletionSource<PipeConnectResult> completion;

        lock (_lock)
        {
            _attemptTimes.Add(_timeProvider.GetUtcNow());

            foreach (var waiter in _waiters.Where(waiter => _attemptTimes.Count >= waiter.Count).ToList())
            {
                waiter.Source.TrySetResult();
                _waiters.Remove(waiter);
            }

            completion = _script.Count > 0 ? _script.Dequeue() : new TaskCompletionSource<PipeConnectResult>();
        }

        if (completion.Task.IsCompleted == false)
        {
            cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
        }

        return new ValueTask<PipeConnectResult>(completion.Task);
    }

    #endregion // IPipeConnector
}