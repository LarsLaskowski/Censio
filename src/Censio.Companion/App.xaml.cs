using System.Windows;
using System.Windows.Threading;

using Censio.Companion.Core;
using Censio.Contracts;

namespace Censio.Companion;

/// <summary>
/// The Companion application.
/// </summary>
public partial class App : Application
{
    #region Fields

    /// <summary>
    /// The guard that allows only one running instance
    /// </summary>
    private SingleInstanceGuard? _guard;

    /// <summary>
    /// The notification area icon
    /// </summary>
    private TrayIcon? _trayIcon;

    /// <summary>
    /// Cancels the pipe connection when the application exits
    /// </summary>
    private CancellationTokenSource? _connectionCancellation;

    #endregion // Fields

    #region Methods

    /// <summary>
    /// Starts the pipe connection and wires its status to the tooltip of the icon.
    /// </summary>
    /// <param name="trayIcon">The notification area icon</param>
    private void StartConnection(TrayIcon trayIcon)
    {
        var connection = new PipeConnection(new NamedPipeConnector(), TimeProvider.System);
        var updater = new TrayStatusUpdater(() => connection.Status,
                                            action => Dispatcher.BeginInvoke(DispatcherPriority.Background, action),
                                            status => trayIcon.SetToolTip(TrayMenu.FormatToolTip(status.State)));

        connection.StatusChanged += (_, _) => updater.Notify();

        _connectionCancellation = new CancellationTokenSource();

        var token = _connectionCancellation.Token;

        Task.Run(() => connection.RunAsync(token), token)
            .ContinueWith(task => _ = task.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
    }

    #endregion // Methods

    #region Application

    /// <inheritdoc />
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _guard = new SingleInstanceGuard(SingleInstanceGuard.DefaultName);

        if (_guard.TryAcquire())
        {
            var trayIcon = new TrayIcon(ProductVersion.Read(typeof(App).Assembly), () => Shutdown(0));

            trayIcon.SetToolTip(TrayMenu.FormatToolTip(PipeConnectionState.WaitingForGame));

            _trayIcon = trayIcon;

            StartConnection(trayIcon);
        }
        else
        {
            Shutdown(0);
        }
    }

    /// <inheritdoc />
    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        _connectionCancellation?.Cancel();
        _connectionCancellation?.Dispose();
        _guard?.Dispose();

        base.OnExit(e);
    }

    #endregion // Application
}