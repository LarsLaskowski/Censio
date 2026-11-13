using Microsoft.Extensions.Logging;

namespace Censio.Server.Tests;

/// <summary>
/// An <see cref="ILogger" /> that keeps every entry written to it from any thread, and lets a test wait for an entry of a
/// level.
/// </summary>
public sealed class ConcurrentRecordingLogger : ILogger
{
    #region Fields

    /// <summary>
    /// Guards the entries and the waiters.
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>
    /// The entries written so far.
    /// </summary>
    private readonly List<(LogLevel Level, string Message, Exception? Exception)> _entries = [];

    /// <summary>
    /// The waiters for an entry of a level.
    /// </summary>
    private readonly List<(LogLevel Level, TaskCompletionSource<(LogLevel Level, string Message, Exception? Exception)> Completion)> _waiters = [];

    #endregion // Fields

    #region Properties

    /// <summary>
    /// Gets a copy of the entries written so far, in the order they were written.
    /// </summary>
    public IReadOnlyList<(LogLevel Level, string Message, Exception? Exception)> Entries
    {
        get
        {
            lock (_lock)
            {
                return [.. _entries];
            }
        }
    }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Waits for the first entry of a level, written before or after the call.
    /// </summary>
    /// <param name="level">The level</param>
    /// <returns>The entry</returns>
    public Task<(LogLevel Level, string Message, Exception? Exception)> WaitForEntryAsync(LogLevel level)
    {
        lock (_lock)
        {
            var written = _entries.FirstOrDefault(entry => entry.Level == level);

            if (written.Message is not null)
            {
                return Task.FromResult(written);
            }

            var completion = new TaskCompletionSource<(LogLevel Level, string Message, Exception? Exception)>(TaskCreationOptions.RunContinuationsAsynchronously);

            _waiters.Add((level, completion));

            return completion.Task;
        }
    }

    #endregion // Methods

    #region ILogger

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        return null;
    }

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var entry = (logLevel, formatter(state, exception), exception);

        lock (_lock)
        {
            _entries.Add(entry);

            foreach (var waiter in _waiters.Where(waiter => waiter.Level == logLevel).ToList())
            {
                _waiters.Remove(waiter);
                waiter.Completion.TrySetResult(entry);
            }
        }
    }

    #endregion // ILogger
}