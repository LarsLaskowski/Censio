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

    /// <summary>
    /// The recording session, <c>null</c> when nothing is recorded
    /// </summary>
    private PipeRecordingSession? _recording;

    #endregion // Fields

    #region Methods

    /// <summary>
    /// Starts the pipe connection and wires its status to the tooltip of the icon.
    /// </summary>
    /// <param name="trayIcon">The notification area icon</param>
    /// <param name="recording">The recording session or <c>null</c> when nothing is recorded</param>
    private void StartConnection(TrayIcon trayIcon, PipeRecordingSession? recording)
    {
        var connection = new PipeConnection(new NamedPipeConnector(), TimeProvider.System, recording);
        var updater = new TrayStatusUpdater<PipeConnectionStatus>(() => connection.Status,
                                                                  action => Dispatcher.BeginInvoke(DispatcherPriority.Background, action),
                                                                  status => trayIcon.SetToolTip(TrayMenu.FormatToolTip(status.State, recording?.State ?? PipeRecordingState.Off)));

        connection.StatusChanged += (_, _) => updater.Notify();

        if (recording is not null)
        {
            recording.StateChanged += (_, _) => updater.Notify();
        }

        Run(connection.RunAsync);
    }

    /// <summary>
    /// Starts the replay of a recording and wires its status to the tooltip of the icon.
    /// </summary>
    /// <param name="trayIcon">The notification area icon</param>
    /// <param name="commandLine">The parsed command line of a replay</param>
    private void StartReplay(TrayIcon trayIcon, CompanionCommandLine commandLine)
    {
        var path = commandLine.ReplayPath!;
        var runner = new PipeReplayRunner(() => System.IO.File.OpenRead(path), commandLine.Speed, TimeProvider.System);
        var updater = new TrayStatusUpdater<PipeReplayStatus>(() => runner.Status,
                                                              action => Dispatcher.BeginInvoke(DispatcherPriority.Background, action),
                                                              status => trayIcon.SetToolTip(TrayMenu.FormatReplayToolTip(status)));

        runner.StatusChanged += (_, _) => updater.Notify();

        trayIcon.SetToolTip(TrayMenu.FormatReplayToolTip(runner.Status));

        Run(runner.RunAsync);
    }

    /// <summary>
    /// Runs a background operation until the application exits.
    /// </summary>
    /// <param name="operation">The operation that takes the cancellation token</param>
    private void Run(Func<CancellationToken, Task> operation)
    {
        _connectionCancellation = new CancellationTokenSource();

        var token = _connectionCancellation.Token;

        Task.Run(() => operation(token), token)
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
            var commandLine = CompanionCommandLine.Parse(e.Args);
            var trayIcon = new TrayIcon(ProductVersion.Read(typeof(App).Assembly), () => Shutdown(0));

            _trayIcon = trayIcon;

            switch (commandLine.Mode)
            {
                case CompanionMode.Invalid:
                    {
                        trayIcon.SetToolTip(TrayMenu.InvalidCommandLineToolTip);
                    }
                    break;

                case CompanionMode.Replay:
                    {
                        StartReplay(trayIcon, commandLine);
                    }
                    break;

                case CompanionMode.Record:
                    {
                        var files = new PipeRecordingFiles(PipeRecordingFiles.DefaultFolder(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)));

                        _recording = new PipeRecordingSession(files.Create);

                        trayIcon.SetToolTip(TrayMenu.FormatToolTip(PipeConnectionState.WaitingForGame, _recording.State));
                        StartConnection(trayIcon, _recording);
                    }
                    break;

                default:
                    {
                        trayIcon.SetToolTip(TrayMenu.FormatToolTip(PipeConnectionState.WaitingForGame));
                        StartConnection(trayIcon, null);
                    }
                    break;
            }
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

        if (_recording is { } recording)
        {
            var stop = Task.Run(() => recording.StopAsync(TimeSpan.FromSeconds(1)), CancellationToken.None);

            stop.Wait(TimeSpan.FromSeconds(1), CancellationToken.None);
        }

        _connectionCancellation?.Dispose();
        _guard?.Dispose();

        base.OnExit(e);
    }

    #endregion // Application
}