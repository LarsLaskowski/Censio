using Microsoft.Extensions.Time.Testing;

namespace Censio.Server.Tests;

/// <summary>
/// A <see cref="FakeTimeProvider" /> whose first read of the current time blocks the reading thread until the test
/// releases it, so that a test acts while the code under test is held at that read. The read is bounded, so a test that
/// never releases it does not hang.
/// </summary>
public sealed class BlockingClockTimeProvider : FakeTimeProvider, IDisposable
{
    #region Fields

    /// <summary>
    /// The longest the first read waits for its release.
    /// </summary>
    private static readonly TimeSpan _bound = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The event that releases the first read.
    /// </summary>
    private readonly ManualResetEventSlim _released = new(false);

    /// <summary>
    /// The source of the task that completes once the first read is held.
    /// </summary>
    private readonly TaskCompletionSource _reached = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// The number of reads of the current time so far.
    /// </summary>
    private int _reads;

    /// <summary>
    /// Whether the first read has returned.
    /// </summary>
    private int _returned;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="BlockingClockTimeProvider" /> class.
    /// </summary>
    /// <param name="start">The time the clock starts at</param>
    public BlockingClockTimeProvider(DateTimeOffset start)
        : base(start)
    {
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the task that completes once the first read of the current time is held.
    /// </summary>
    public Task Reached => _reached.Task;

    /// <summary>
    /// Gets a value indicating whether the first read of the current time has returned.
    /// </summary>
    public bool HasReturned => Volatile.Read(ref _returned) == 1;

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Releases the first read of the current time, also one that has not started yet.
    /// </summary>
    public void Release()
    {
        _released.Set();
    }

    #endregion // Methods

    #region TimeProvider

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow()
    {
        if (Interlocked.Increment(ref _reads) > 1)
        {
            return base.GetUtcNow();
        }

        _reached.TrySetResult();
        _released.Wait(_bound);

        var now = base.GetUtcNow();

        Volatile.Write(ref _returned, 1);

        return now;
    }

    #endregion // TimeProvider

    #region IDisposable

    /// <inheritdoc/>
    public void Dispose()
    {
        _released.Dispose();
    }

    #endregion // IDisposable
}