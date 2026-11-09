using System.Windows.Forms;

using Censio.Companion.Core;

namespace Censio.Companion;

/// <summary>
/// The notification area icon with its context menu.
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    #region Fields

    /// <summary>
    /// The notification area icon
    /// </summary>
    private readonly NotifyIcon _notifyIcon;

    /// <summary>
    /// The context menu of the icon
    /// </summary>
    private readonly ContextMenuStrip _menu;

    /// <summary>
    /// The icons of the looks
    /// </summary>
    private readonly TrayIconImages _images;

    /// <summary>
    /// The product version
    /// </summary>
    private readonly string _version;

    /// <summary>
    /// Gets the current snapshot
    /// </summary>
    private readonly Func<TraySnapshot> _snapshot;

    /// <summary>
    /// Gets whether the Companion starts with Windows
    /// </summary>
    private readonly Func<bool> _isAutostartEnabled;

    /// <summary>
    /// Gets the status of the game installation search
    /// </summary>
    private readonly Func<GameInstallationStatus> _game;

    /// <summary>
    /// Gets the status of the game catalog
    /// </summary>
    private readonly Func<GameCatalogStatus> _catalog;

    /// <summary>
    /// Runs the action of a menu entry
    /// </summary>
    private readonly Action<TrayMenuEntryKind> _dispatch;

    /// <summary>
    /// Whether the icon is disposed
    /// </summary>
    private bool _disposed;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="TrayIcon" /> class.
    /// </summary>
    /// <param name="version">The product version</param>
    /// <param name="images">The icons of the looks</param>
    /// <param name="snapshot">Gets the current snapshot</param>
    /// <param name="game">Gets the status of the game installation search</param>
    /// <param name="catalog">Gets the status of the game catalog</param>
    /// <param name="isAutostartEnabled">Gets whether the Companion starts with Windows</param>
    /// <param name="dispatch">Runs the action of a menu entry</param>
    public TrayIcon(string version, TrayIconImages images, Func<TraySnapshot> snapshot, Func<GameInstallationStatus> game, Func<GameCatalogStatus> catalog, Func<bool> isAutostartEnabled, Action<TrayMenuEntryKind> dispatch)
    {
        _version = version;
        _images = images;
        _snapshot = snapshot;
        _game = game;
        _catalog = catalog;
        _isAutostartEnabled = isAutostartEnabled;
        _dispatch = dispatch;
        _menu = new ContextMenuStrip();

        BuildItems();

        _menu.Opening += (_, e) =>
                         {
                             BuildItems();
                             e.Cancel = false;
                         };

        _notifyIcon = new NotifyIcon
                      {
                          Icon = images.Get(TrayIconLook.Waiting),
                          Text = TrayMenu.ToolTip,
                          ContextMenuStrip = _menu,
                          Visible = true
                      };
    }

    #endregion // Constructors

    #region Methods

    /// <summary>
    /// Sets the tooltip of the icon; does nothing once the icon is disposed.
    /// </summary>
    /// <param name="text">The tooltip text</param>
    public void SetToolTip(string text)
    {
        if (_disposed)
        {
            return;
        }

        _notifyIcon.Text = text.Length > TrayMenu.MaxToolTipLength ? text[..TrayMenu.MaxToolTipLength] : text;
    }

    /// <summary>
    /// Sets the look of the icon; does nothing once the icon is disposed.
    /// </summary>
    /// <param name="look">The look</param>
    public void SetLook(TrayIconLook look)
    {
        if (_disposed)
        {
            return;
        }

        _notifyIcon.Icon = _images.Get(look);
    }

    /// <summary>
    /// Replaces the items of the menu by the entries of the current snapshot, disposing the old ones.
    /// </summary>
    private void BuildItems()
    {
        var old = _menu.Items.Cast<ToolStripItem>().ToList();

        _menu.Items.Clear();

        foreach (var entry in TrayMenu.Create(_version, _snapshot(), _game(), _catalog(), _isAutostartEnabled(), TimeProvider.System))
        {
            if (entry.Kind == TrayMenuEntryKind.Separator)
            {
                _menu.Items.Add(new ToolStripSeparator());

                continue;
            }

            var item = new ToolStripMenuItem(entry.Text)
                       {
                           Enabled = entry.IsEnabled,
                           Checked = entry.IsChecked
                       };
            var kind = entry.Kind;

            if (kind is TrayMenuEntryKind.OpenDiagnostics or TrayMenuEntryKind.ToggleRecording or TrayMenuEntryKind.OpenRecordingsFolder or TrayMenuEntryKind.OpenLogFolder or TrayMenuEntryKind.ToggleAutostart or TrayMenuEntryKind.Exit)
            {
                item.Click += (_, _) => _dispatch(kind);
            }

            _menu.Items.Add(item);
        }

        foreach (var item in old)
        {
            item.Dispose();
        }
    }

    #endregion // Methods

    #region IDisposable

    /// <inheritdoc />
    public void Dispose()
    {
        _disposed = true;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _menu.Dispose();
    }

    #endregion // IDisposable
}