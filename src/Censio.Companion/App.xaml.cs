using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

using Censio.Companion.Core;
using Censio.Contracts;
using Censio.GameData;
using Censio.Pipe;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Censio.Companion;

/// <summary>
/// The Companion application.
/// </summary>
public sealed partial class App : Application, IDisposable
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
    /// The search of the game installation.
    /// </summary>
    private GameInstallationSearch? _gameSearch;

    /// <summary>
    /// The loader of the game catalog.
    /// </summary>
    private GameCatalogLoader? _catalogLoader;

    /// <summary>
    /// The check of the GUIDs of closed ticks against the catalog.
    /// </summary>
    private GameCatalogReporter? _catalogReporter;

    /// <summary>
    /// Reads the latest closed tick of the source, <c>null</c> until a source is running.
    /// </summary>
    private volatile Func<PipeTick?>? _readLatestTick;

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
    /// The session of the diagnostics window
    /// </summary>
    private DiagnosticsWindowSession? _diagnostics;

    /// <summary>
    /// The rules of the start with Windows
    /// </summary>
    private CompanionAutostart? _autostart;

    /// <summary>
    /// Whether the application is exiting
    /// </summary>
    private volatile bool _exiting;

    /// <summary>
    /// Whether the application is disposed
    /// </summary>
    private int _disposed;

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
    /// Creates the session of the diagnostics window.
    /// </summary>
    /// <param name="readTick">Reads the latest closed tick</param>
    private void CreateDiagnostics(Func<PipeTick?> readTick)
    {
        _diagnostics = new DiagnosticsWindowSession(() => _snapshot().StatusText,
                                                    readTick,
                                                    () => _catalogLoader?.Catalog,
                                                    () => _catalogLoader?.Icons,
                                                    TimeProvider.System,
                                                    action => Dispatcher.BeginInvoke(DispatcherPriority.Background, action),
                                                    () => new DiagnosticsWindow());
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
                                                             _diagnostics?.Notify();
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

        CreateDiagnostics(() => connection.LatestTick);
        _readLatestTick = () => connection.LatestTick;

        var trayIcon = CreateTrayIcon();
        var updater = CreateUpdater(trayIcon);

        connection.StatusChanged += (_, status) => CheckCatalog(status);
        connection.StatusChanged += (_, _) => Notify(updater);
        connection.TickReceived += (_, tick) => _catalogReporter?.Report(tick);
        connection.TickReceived += (_, _) => Notify(updater);
        recording.StateChanged += (_, _) => Notify(updater);
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
        var runner = commandLine.ServerAddress is { } address
                         ? new PipeReplayRunner(() => System.IO.File.OpenRead(path),
                                                commandLine.Speed,
                                                TimeProvider.System,
                                                _logger,
                                                new ReplaySender(address, commandLine.TokenFilePath, ProductVersion.Read(typeof(App).Assembly), new ClientWebSocketConnector(TimeProvider.System), TimeProvider.System, _logger))
                         : new PipeReplayRunner(() => System.IO.File.OpenRead(path), commandLine.Speed, TimeProvider.System, _logger);
        var fileName = System.IO.Path.GetFileName(path);

        CompanionLog.ReplayStarted(_logger, fileName);

        _snapshot = () => TraySnapshot.ForReplay(runner.Status, runner.LastTick);

        CreateDiagnostics(() => runner.LatestTick);
        _readLatestTick = () => runner.LatestTick;

        var trayIcon = CreateTrayIcon();
        var updater = CreateUpdater(trayIcon);

        runner.StatusChanged += (_, _) => Notify(updater);
        runner.TickReceived += (_, tick) => _catalogReporter?.Report(tick);
        runner.TickReceived += (_, _) => Notify(updater);
        updater.Notify();

        Run(runner.RunAsync);
    }

    /// <summary>
    /// Lets the catalog loader check the game for a change when the pipe connected.
    /// </summary>
    /// <param name="status">The status of the pipe connection</param>
    private void CheckCatalog(PipeConnectionStatus status)
    {
        if (_catalogLoader is { } loader && _gameSearch is { } search)
        {
            Run(_ => loader.OnConnectionStatusChangedAsync(status, search.RefreshBuild));
        }
    }

    /// <summary>
    /// Refreshes the diagnostics window when the status of the catalog changed.
    /// </summary>
    /// <param name="sender">The loader</param>
    /// <param name="status">The new status</param>
    private void OnCatalogStatusChanged(object? sender, GameCatalogStatus status)
    {
        _diagnostics?.Notify();
    }

    /// <summary>
    /// Checks the latest tick against a catalog that was published and refreshes the diagnostics window.
    /// </summary>
    /// <param name="sender">The loader</param>
    /// <param name="catalog">The published catalog, or <c>null</c> when there is none</param>
    private void OnCatalogChanged(object? sender, GameCatalog? catalog)
    {
        if (catalog is not null && _readLatestTick?.Invoke() is { } tick)
        {
            _catalogReporter?.Report(tick);
        }

        _diagnostics?.Notify();
    }

    /// <summary>
    /// Announces the icons to the diagnostics window.
    /// </summary>
    /// <param name="sender">The loader</param>
    /// <param name="eventArgs">The event data</param>
    private void OnCatalogIconsChanged(object? sender, EventArgs eventArgs)
    {
        _diagnostics?.Notify();
    }

    /// <summary>
    /// Announces a change to the tray and to the diagnostics window.
    /// </summary>
    /// <param name="updater">The updater of the tray</param>
    private void Notify(TrayStatusUpdater<TraySnapshot> updater)
    {
        updater.Notify();
        _diagnostics?.Notify();
    }

    /// <summary>
    /// Creates the notification area icon.
    /// </summary>
    /// <returns>The icon</returns>
    private TrayIcon CreateTrayIcon()
    {
        _images = new TrayIconImages();
        _trayIcon = new TrayIcon(ProductVersion.Read(typeof(App).Assembly), _images, () => _snapshot(), () => _gameSearch?.Status ?? GameInstallationStatus.Searching, () => _catalogLoader?.Status ?? GameCatalogStatus.Reading, () => _autostart?.Refresh() ?? false, Dispatch);

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
            case TrayMenuEntryKind.OpenDiagnostics:
                {
                    _diagnostics?.Open();
                }
                break;

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

            case TrayMenuEntryKind.ToggleAutostart:
                {
                    _autostart?.Toggle();
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
            _autostart = new CompanionAutostart(new RegistryStartupEntryStore(), Environment.ProcessPath, _logger);
            _folderOpener = new FolderOpener(LaunchExplorer, _logger);
            _connectionCancellation = new CancellationTokenSource();

            var version = ProductVersion.Read(typeof(App).Assembly);

            CompanionLog.Started(_logger, version, commandLine.Mode);

            var gameSearch = new GameInstallationSearch(new RegistryUbisoftInstallRecords(), new WindowsFileVersionReader(), GameInstallation.DefaultUbisoftGamesFolder(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)), _logger);
            var settingsFile = CompanionFolders.Settings(localAppData);

            var catalogLoader = new GameCatalogLoader(folder => GameCatalogReader.Load(folder, RdaLimits.Default), GameIconReader.Load, status => GameCatalogKeyReader.Read(status, CompanionIdentity.Current), new GameCatalogCacheStore(CompanionFolders.Cache(localAppData), TimeProvider.System), TimeProvider.System, _logger);

            _gameSearch = gameSearch;
            _catalogLoader = catalogLoader;
            _catalogReporter = new GameCatalogReporter(() => catalogLoader.Catalog, _logger);
            catalogLoader.StatusChanged += OnCatalogStatusChanged;
            catalogLoader.CatalogChanged += OnCatalogChanged;
            catalogLoader.IconsChanged += OnCatalogIconsChanged;

            Run(_ =>
                {
                    catalogLoader.Run(gameSearch.Run(settingsFile));

                    return Task.CompletedTask;
                });

            switch (commandLine.Mode)
            {
                case CompanionMode.Invalid:
                    {
                        CreateDiagnostics(() => null);

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
        Dispose();

        base.OnExit(e);
    }

    #endregion // Application

    #region IDisposable

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

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
        }

        _logProvider?.Dispose();

        _images?.Dispose();
        _connectionCancellation?.Dispose();
        _guard?.Dispose();
    }

    #endregion // IDisposable
}