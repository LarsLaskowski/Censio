namespace Censio.Pipe;

/// <summary>
/// A timer that never fires.
/// </summary>
internal sealed class PipeReplayTimer : ITimer
{
    #region ITimer

    /// <inheritdoc />
    public bool Change(TimeSpan dueTime, TimeSpan period)
    {
        return true;
    }

    #endregion // ITimer

    #region IDisposable

    /// <inheritdoc />
    public void Dispose()
    {
    }

    #endregion // IDisposable

    #region IAsyncDisposable

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    #endregion // IAsyncDisposable
}