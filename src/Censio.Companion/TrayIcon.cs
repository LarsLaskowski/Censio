using System.Windows.Forms;

using Censio.Companion.Core;

namespace Censio.Companion;

/// <summary>
/// The notification area icon with its context menu.
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    #region Constants

    /// <summary>
    /// The longest title of a notification in characters
    /// </summary>
    private const int MaxNoticeTitleLength = 63;

    /// <summary>
    /// The longest text of a notification in characters
    /// </summary>
    private const int MaxNoticeTextLength = 255;

    /// <summary>
    /// The time a notification asks to be shown in milliseconds; Windows decides
    /// </summary>
    private const int NoticeTimeout = 10000;

    #endregion // Constants

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
    /// Reads what the icon shows
    /// </summary>
    private readonly TrayIconSources _sources;

    /// <summary>
    /// Runs the action of a menu entry
    /// </summary>
    private readonly Action<TrayMenuEntryKind> _dispatch;

    /// <summary>
    /// Whether the icon is disposed
    /// </summary>
    private bool _disposed;

    #endregion // Fields

    #region Events

    /// <summary>
    /// Occurs when the user clicked a notification.
    /// </summary>
    public event EventHandler? NoticeClicked;

    #endregion // Events

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="TrayIcon" /> class.
    /// </summary>
    /// <param name="version">The product version</param>
    /// <param name="images">The icons of the looks</param>
    /// <param name="sources">Reads what the icon shows</param>
    /// <param name="dispatch">Runs the action of a menu entry</param>
    public TrayIcon(string version, TrayIconImages images, TrayIconSources sources, Action<TrayMenuEntryKind> dispatch)
    {
        _version = version;
        _images = images;
        _sources = sources;
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
                          Text = sources.Texts().ToolTip,
                          ContextMenuStrip = _menu,
                          Visible = true
                      };

        _notifyIcon.BalloonTipClicked += (_, _) => NoticeClicked?.Invoke(this, EventArgs.Empty);
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
    /// Shows a notification; does nothing once the icon is disposed.
    /// </summary>
    /// <param name="title">The title</param>
    /// <param name="text">The text</param>
    public void ShowNotice(string title, string text)
    {
        if (_disposed)
        {
            return;
        }

        _notifyIcon.ShowBalloonTip(NoticeTimeout, title.Length > MaxNoticeTitleLength ? title[..MaxNoticeTitleLength] : title, text.Length > MaxNoticeTextLength ? text[..MaxNoticeTextLength] : text, ToolTipIcon.Info);
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

        foreach (var entry in TrayMenu.Create(_sources.Texts(),
                                              new TrayMenuState
                                              {
                                                  Version = _version,
                                                  Snapshot = _sources.Snapshot(),
                                                  Game = _sources.Game(),
                                                  Catalog = _sources.Catalog(),
                                                  Server = _sources.Server(),
                                                  IsAutostartEnabled = _sources.IsAutostartEnabled(),
                                                  Clock = TimeProvider.System
                                              }))
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

            if (kind is TrayMenuEntryKind.OpenCensio or TrayMenuEntryKind.OpenSettings or TrayMenuEntryKind.OpenDiagnostics or TrayMenuEntryKind.ToggleRecording or TrayMenuEntryKind.OpenRecordingsFolder or TrayMenuEntryKind.OpenLogFolder or TrayMenuEntryKind.ToggleAutostart or TrayMenuEntryKind.Exit)
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