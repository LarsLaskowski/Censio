using Censio.Server;

using Microsoft.Extensions.Logging;

namespace Censio.Companion.Core;

/// <summary>
/// Hosts the local server and tracks its status.
/// </summary>
public sealed class LocalServerSession : IAsyncDisposable
{
    #region Fields

#pragma warning disable S4487 // Skeleton: removed with the implementation

    /// <summary>
    /// The setting.
    /// </summary>
    private readonly LocalServerSetting _setting;

    /// <summary>
    /// The delegate that starts the server.
    /// </summary>
    private readonly Func<int, CancellationToken, Task<LocalServerStart>> _start;

    /// <summary>
    /// The time provider.
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// The logger.
    /// </summary>
    private readonly ILogger _logger;

#pragma warning restore S4487 // Skeleton: removed with the implementation

    #endregion // Fields

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalServerSession" /> class.
    /// </summary>
    /// <param name="setting">The setting</param>
    /// <param name="start">The delegate that starts the server on a port</param>
    /// <param name="timeProvider">The time provider</param>
    /// <param name="logger">The logger</param>
    public LocalServerSession(LocalServerSetting setting, Func<int, CancellationToken, Task<LocalServerStart>> start, TimeProvider timeProvider, ILogger logger)
    {
        _setting = setting;
        _start = start;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    #endregion // Constructor

    #region Events

    /// <summary>
    /// Occurs when the status changes.
    /// </summary>
#pragma warning disable CS0067, S3264 // Skeleton: removed with the implementation
    public event EventHandler<LocalServerStatus>? StatusChanged;
#pragma warning restore CS0067, S3264 // Skeleton: removed with the implementation

    #endregion // Events

    #region Properties

    /// <summary>
    /// Gets the status.
    /// </summary>
    public LocalServerStatus Status => new()
                                       {
                                           State = LocalServerState.Starting,
                                           Port = _setting.Port
                                       };

    /// <summary>
    /// Gets the address of the server.
    /// </summary>
    public Uri Address => new(_setting.Problem ?? "http://localhost/");

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Determines whether the Companion hosts a local server.
    /// </summary>
    /// <param name="mode">The mode</param>
    /// <param name="setting">The setting</param>
    /// <returns><see langword="true" /> when a server is hosted</returns>
    public static bool IsHosted(CompanionMode mode, LocalServerSetting setting)
    {
#pragma warning disable RH2003, S1172 // Skeleton: removed with the implementation
        throw new NotImplementedException();
#pragma warning restore RH2003, S1172 // Skeleton: removed with the implementation
    }

    /// <summary>
    /// Starts the server and runs until it ends.
    /// </summary>
    /// <param name="cancellationToken">The token that cancels the run</param>
    /// <returns>A task that completes when the run ends</returns>
#pragma warning disable S2325 // Skeleton: removed with the implementation
    public Task RunAsync(CancellationToken cancellationToken)
    {
#pragma warning disable RH2003, S1172 // Skeleton: removed with the implementation
        throw new NotImplementedException();
#pragma warning restore RH2003, S1172 // Skeleton: removed with the implementation
    }
#pragma warning restore S2325 // Skeleton: removed with the implementation

    /// <summary>
    /// Waits for the token of the running server.
    /// </summary>
    /// <param name="cancellationToken">The token that cancels the wait</param>
    /// <returns>The token, or <see langword="null" /> when the server does not run</returns>
#pragma warning disable S2325 // Skeleton: removed with the implementation
    public Task<string?> WaitForTokenAsync(CancellationToken cancellationToken)
    {
#pragma warning disable RH2003, S1172 // Skeleton: removed with the implementation
        throw new NotImplementedException();
#pragma warning restore RH2003, S1172 // Skeleton: removed with the implementation
    }
#pragma warning restore S2325 // Skeleton: removed with the implementation

    /// <summary>
    /// Stops the server.
    /// </summary>
    /// <param name="timeout">The longest time to wait</param>
    /// <returns>A task that completes when the server has stopped or the time is over</returns>
#pragma warning disable S2325 // Skeleton: removed with the implementation
    public Task StopAsync(TimeSpan timeout)
    {
#pragma warning disable RH2003, S1172 // Skeleton: removed with the implementation
        throw new NotImplementedException();
#pragma warning restore RH2003, S1172 // Skeleton: removed with the implementation
    }
#pragma warning restore S2325 // Skeleton: removed with the implementation

    #endregion // Methods

    #region IAsyncDisposable

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    #endregion // IAsyncDisposable
}