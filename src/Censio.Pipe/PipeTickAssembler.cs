namespace Censio.Pipe;

/// <summary>
/// Reads messages from a source and delivers them as ticks and session events on an injected clock.
/// </summary>
public sealed class PipeTickAssembler : IDisposable
{
    #region Fields

    /// <summary>
    /// The source of the messages.
    /// </summary>
    private readonly IPipeMessageSource _source;

    /// <summary>
    /// The clock that stamps the receive times and times the quiet period.
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// The items produced but not yet delivered.
    /// </summary>
    private readonly Queue<PipeTickResult> _queue = new();

    /// <summary>
    /// The cancellation of the source reads.
    /// </summary>
    private readonly CancellationTokenSource _readCancellation = new();

    /// <summary>
    /// The builder that groups the results.
    /// </summary>
    private PipeTickBuilder _builder = new();

    /// <summary>
    /// The read that has not been fed to the builder yet.
    /// </summary>
    private Task<(PipeReadResult Result, DateTimeOffset Stamp)>? _pending;

    /// <summary>
    /// Whether the stream has ended for this assembler.
    /// </summary>
    private bool _ended;

    /// <summary>
    /// Whether the assembler was disposed.
    /// </summary>
    private bool _disposed;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PipeTickAssembler" /> class.
    /// </summary>
    /// <param name="source">The source of the messages</param>
    /// <param name="timeProvider">The clock</param>
    public PipeTickAssembler(IPipeMessageSource source, TimeProvider timeProvider)
    {
        _source = source;
        _timeProvider = timeProvider;
    }

    #endregion // Constructors

    #region Methods

    /// <summary>
    /// Reads the next tick or session event.
    /// </summary>
    /// <param name="cancellationToken">The token that cancels the read</param>
    /// <returns>The next result, or the end of the stream</returns>
    /// <exception cref="OperationCanceledException">The read was cancelled.</exception>
    /// <exception cref="ObjectDisposedException">The assembler was disposed.</exception>
    public async ValueTask<PipeTickResult> ReadAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            while (_queue.Count == 0)
            {
                if (_ended)
                {
                    return PipeTickResult.End;
                }

                cancellationToken.ThrowIfCancellationRequested();

                await Step(cancellationToken).ConfigureAwait(false);
            }

            return _queue.Dequeue();
        }
        catch (OperationCanceledException)
        {
            Abandon();

            throw;
        }
    }

    /// <summary>
    /// Performs one pass of the wait loop (feed a completed read, wait for a read, close a quiet tick or race the quiet period).
    /// </summary>
    /// <param name="cancellationToken">The token that cancels the read</param>
    /// <returns>The task</returns>
    private async Task Step(CancellationToken cancellationToken)
    {
        _pending ??= ReadStampedAsync(_readCancellation.Token);

        if (_pending.IsCompleted)
        {
            await Feed().ConfigureAwait(false);

            return;
        }

        var deadline = _builder.QuietDeadline;

        if (deadline is null)
        {
            await _pending.WaitAsync(cancellationToken).ConfigureAwait(false);
            await Feed().ConfigureAwait(false);

            return;
        }

        var now = _timeProvider.GetUtcNow();

        if (now >= deadline.Value)
        {
            Enqueue(_builder.CloseIfQuiet(now));

            return;
        }

        using var delaySource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var delay = Task.Delay(deadline.Value - now, _timeProvider, delaySource.Token);

        await Task.WhenAny(_pending, delay).ConfigureAwait(false);
        await delaySource.CancelAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Feeds the completed read to the builder and queues what it produces.
    /// </summary>
    /// <returns>The task</returns>
    private async Task Feed()
    {
        var (result, stamp) = await _pending!.ConfigureAwait(false);

        _pending = null;

        foreach (var item in _builder.Add(result, stamp))
        {
            _queue.Enqueue(item);

            if (item.Kind == PipeTickResultKind.EndOfStream)
            {
                _ended = true;
            }
        }
    }

    /// <summary>
    /// Queues an item when there is one.
    /// </summary>
    /// <param name="item">The item</param>
    private void Enqueue(PipeTickResult? item)
    {
        if (item is not null)
        {
            _queue.Enqueue(item);
        }
    }

    /// <summary>
    /// Reads one result from the source and stamps it with the time the read completed.
    /// </summary>
    /// <param name="cancellationToken">The token of the source read</param>
    /// <returns>The result and its receive time</returns>
    private async Task<(PipeReadResult Result, DateTimeOffset Stamp)> ReadStampedAsync(CancellationToken cancellationToken)
    {
        var result = await _source.ReadAsync(cancellationToken).ConfigureAwait(false);

        return (result, _timeProvider.GetUtcNow());
    }

    /// <summary>
    /// Ends the stream after a cancellation: cancels the source read and discards the open tick and the queue.
    /// </summary>
    private void Abandon()
    {
        _ended = true;
        _queue.Clear();
        _builder = new PipeTickBuilder();

        if (_disposed == false)
        {
            _readCancellation.Cancel();
        }

        // A read that ends cancelled or failed is observed here, so it never goes unobserved
        _pending?.ContinueWith(static task => _ = task.Exception, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        _pending = null;
    }

    #endregion // Methods

    #region IDisposable

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _readCancellation.Cancel();
        _pending?.ContinueWith(static task => _ = task.Exception, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        _disposed = true;
        _readCancellation.Dispose();
    }

    #endregion // IDisposable
}