using System.Windows;

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

    #endregion // Fields

    #region Application

    /// <inheritdoc />
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _guard = new SingleInstanceGuard(SingleInstanceGuard.DefaultName);

        if (_guard.TryAcquire())
        {
            _trayIcon = new TrayIcon(ProductVersion.Read(typeof(App).Assembly), () => Shutdown(0));
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
        _guard?.Dispose();

        base.OnExit(e);
    }

    #endregion // Application
}