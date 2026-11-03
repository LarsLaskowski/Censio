using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

using Censio.Companion.Core;
using Censio.Contracts;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
    /// The icons of the looks of the notification area icon
    /// </summary>
    private TrayIconImages? _images;

    /// <summary>
    /// Cancels the pipe connection when the application exits
    /// </summary>
    private CancellationTokenSource? _connectionCancellation;

    /// <summary>
    /// The recording session, <c>null</c> in a replay or with an invalid command line
    /// </summary>
    private PipeRecordingSession? _recording;

    /// <summary>
    /// The log file writer
    /// </summary>
    private LogFileLoggerProvider? _logProvider;

    /// <summary>
    /// The logger of the Companion
    /// </summary>
    private ILogger _logger = NullLogger.Instance;

    /// <summary>
    /// The opener of the recordings and log folders
    /// </summary>
    private FolderOpener? _folderOpener;

    /// <summary>
    /// The recordings folder
    /// </summary>
    private string _recordingsFolder = string.Empty;

    /// <summary>
    /// The log folder
    /// </summary>
    private string _logFolder = string.Empty;

    /// <summary>
    /// The timer that refreshes the tray at the next local midnight
    /// </summary>
    private ITimer? _midnightTimer;

    /// <summary>
    /// Gets the snapshot of the tray
    /// </summary>
    private Func<TraySnapshot> _snapshot = TraySnapshot.ForInvalidCommandLine;

    /// <summary>
    /// Whether the application is exiting
    /// </summary>
    private volatile bool _exiting;

    #endregion // Fields

    #region Methods

    /// <summary>
    /// Starts Explorer on a folder; the folder is one argument and no shell is involved.
    /// </summary>
    /// <param name="folder">The folder</param>
    private static void LaunchExplorer(string folder)
    {
        var start = new ProcessStartInfo(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"))
                    {
                        UseShellExecute = false
                    };

        start.ArgumentList.Add(folder);

        using var process = Process.Start(start);
    }

    /// <summary>
    /// Creates the updater of the tray and the timer that refreshes it at local midnight.
    /// </summary>
    /// <param name="trayIcon">The notification area icon</param>
    /// <returns>The updater</returns>
    private TrayStatusUpdater<TraySnapshot> CreateUpdater(TrayIcon trayIcon)
    {
        var updater = new TrayStatusUpdater<TraySnapshot>(() => _snapshot(),
                                                          action => Dispatcher.BeginInvoke(DispatcherPriority.Background, action),
                                                          snapshot =>
                                                          {
                                                              trayIcon.SetToolTip(TrayMenu.FormatSnapshotToolTip(snapshot, TimeProvider.System));
                                                              trayIcon.SetLook(snapshot.Look);
                                                          });

        _midnightTimer = TimeProvider.System.CreateTimer(_ =>
                                                         {
                                                             updater.Notify();
                                                             ArmMidnightTimer();
                                                         },
                                                         null,
                                                         TrayMenu.UntilNextLocalMidnight(TimeProvider.System),
                                                         Timeout.InfiniteTimeSpan);

        return updater;
    }

    /// <summary>
    /// Arms the midnight timer for the next local midnight.
    /// </summary>
    private void ArmMidnightTimer()
    {
        if (_exiting)
        {
            return;
        }

        try
        {
            _midnightTimer?.Change(TrayMenu.UntilNextLocalMidnight(TimeProvider.System), Timeout.InfiniteTimeSpan);
        }
        catch (ObjectDisposedException)
        {
            // The application is exiting
        }
    }

    /// <summary>
    /// Starts the pipe connection and wires its status to the tray.
    /// </summary>
    /// <param name="recording">The recording session</param>
    /// <param name="start">Whether the recording starts at once</param>
    private void StartConnection(PipeRecordingSession recording, bool start)
    {
        if (start)
        {
            recording.Start();
        }

        var connection = new PipeConnection(new NamedPipeConnector(), TimeProvider.System, recording, _logger);

        _snapshot = () => TraySnapshot.ForConnection(connection.Status, recording.State, connection.LastTick);

        var trayIcon = CreateTrayIcon();
        var updater = CreateUpdater(trayIcon);

        connection.StatusChanged += (_, _) => updater.Notify();
        connection.TickReceived += (_, _) => updater.Notify();
        recording.StateChanged += (_, _) => updater.Notify();
        updater.Notify();

        Run(connection.RunAsync);
    }

    /// <summary>
    /// Starts the replay of a recording and wires its status to the tray.
    /// </summary>
    /// <param name="commandLine">The parsed command line of a replay</param>
    private void StartReplay(CompanionCommandLine commandLine)
    {
        var path = commandLine.ReplayPath!;
        var runner = new PipeReplayRunner(() => System.IO.File.OpenRead(path), commandLine.Speed, TimeProvider.System, _logger);
        var fileName = System.IO.Path.GetFileName(path);

        CompanionLog.ReplayStarted(_logger, fileName);

        _snapshot = () => TraySnapshot.ForReplay(runner.Status, runner.LastTick);

        var trayIcon = CreateTrayIcon();
        var updater = CreateUpdater(trayIcon);

        runner.StatusChanged += (_, _) => updater.Notify();
        runner.TickReceived += (_, _) => updater.Notify();
        updater.Notify();

        Run(runner.RunAsync);
    }

    /// <summary>
    /// Creates the notification area icon.
    /// </summary>
    /// <returns>The icon</returns>
    private TrayIcon CreateTrayIcon()
    {
        _images = new TrayIconImages();
        _trayIcon = new TrayIcon(ProductVersion.Read(typeof(App).Assembly), _images, () => _snapshot(), Dispatch);

        return _trayIcon;
    }

    /// <summary>
    /// Runs the action of a menu entry.
    /// </summary>
    /// <param name="kind">The kind of the entry</param>
    private void Dispatch(TrayMenuEntryKind kind)
    {
        switch (kind)
        {
            case TrayMenuEntryKind.ToggleRecording:
                {
                    _recording?.Toggle();
                }
                break;

            case TrayMenuEntryKind.OpenRecordingsFolder:
                {
                    OpenFolder(_recordingsFolder, "recordings");
                }
                break;

            case TrayMenuEntryKind.OpenLogFolder:
                {
                    OpenFolder(_logFolder, "log");
                }
                break;

            case TrayMenuEntryKind.Exit:
                {
                    Shutdown(0);
                }
                break;
        }
    }

    /// <summary>
    /// Opens a folder in Explorer off the UI thread.
    /// </summary>
    /// <param name="folder">The folder</param>
    /// <param name="displayName">The name of the folder in the log</param>
    private void OpenFolder(string folder, string displayName)
    {
        var opener = _folderOpener;

        if (opener is null)
        {
            return;
        }

        Run(_ =>
            {
                opener.Open(folder, displayName);

                return Task.CompletedTask;
            });
    }

    /// <summary>
    /// Runs a background operation until the application exits and logs it if it fails.
    /// </summary>
    /// <param name="operation">The operation that takes the cancellation token</param>
    private void Run(Func<CancellationToken, Task> operation)
    {
        var token = _connectionCancellation!.Token;

        Task.Run(() => operation(token), token)
            .ContinueWith(task => CompanionLog.BackgroundTaskFailed(_logger, task.Exception!), CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
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
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            _recordingsFolder = PipeRecordingFiles.DefaultFolder(localAppData);
            _logFolder = CompanionFolders.Logs(localAppData);
            _logProvider = new LogFileLoggerProvider(_logFolder, TimeProvider.System);
            _logger = _logProvider.CreateLogger("Censio.Companion");
            _folderOpener = new FolderOpener(LaunchExplorer, _logger);
            _connectionCancellation = new CancellationTokenSource();

            var version = ProductVersion.Read(typeof(App).Assembly);

            CompanionLog.Started(_logger, version, commandLine.Mode);

            switch (commandLine.Mode)
            {
                case CompanionMode.Invalid:
                    {
                        var trayIcon = CreateTrayIcon();
                        var updater = CreateUpdater(trayIcon);

                        updater.Notify();
                    }
                    break;

                case CompanionMode.Replay:
                    {
                        StartReplay(commandLine);
                    }
                    break;

                default:
                    {
                        _recording = new PipeRecordingSession(new PipeRecordingFiles(_recordingsFolder, _logger).Create, _logger);

                        StartConnection(_recording, commandLine.Mode == CompanionMode.Record);
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
        _exiting = true;

        if (_logProvider is not null)
        {
            CompanionLog.Exiting(_logger);
        }

        _trayIcon?.Dispose();
        _midnightTimer?.Dispose();
        _connectionCancellation?.Cancel();

        if (_recording is { } recording)
        {
            var stop = Task.Run(() => recording.StopAsync(TimeSpan.FromSeconds(1)), CancellationToken.None);

            stop.Wait(TimeSpan.FromSeconds(1), CancellationToken.None);
        }

        if (_logProvider is { } provider)
        {
            var flush = Task.Run(() => provider.FlushAsync(TimeSpan.FromMilliseconds(500)), CancellationToken.None);

            flush.Wait(TimeSpan.FromMilliseconds(500), CancellationToken.None);
            provider.Dispose();
        }

        _images?.Dispose();
        _connectionCancellation?.Dispose();
        _guard?.Dispose();

        base.OnExit(e);
    }

    #endregion // Application
}