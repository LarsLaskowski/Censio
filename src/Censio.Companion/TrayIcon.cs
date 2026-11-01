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
    /// Whether the icon is disposed
    /// </summary>
    private bool _disposed;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="TrayIcon" /> class.
    /// </summary>
    /// <param name="version">The product version</param>
    /// <param name="exit">The action the exit entry runs</param>
    public TrayIcon(string version, Action exit)
    {
        _menu = new ContextMenuStrip();

        foreach (var entry in TrayMenu.Create(version))
        {
            switch (entry.Kind)
            {
                case TrayMenuEntryKind.Separator:
                    {
                        _menu.Items.Add(new ToolStripSeparator());
                    }
                    break;

                case TrayMenuEntryKind.Exit:
                    {
                        var item = new ToolStripMenuItem(entry.Text)
                                   {
                                       Enabled = entry.IsEnabled
                                   };

                        item.Click += (_, _) => exit();

                        _menu.Items.Add(item);
                    }
                    break;

                default:
                    {
                        _menu.Items.Add(new ToolStripMenuItem(entry.Text)
                                        {
                                            Enabled = entry.IsEnabled
                                        });
                    }
                    break;
            }
        }

        _notifyIcon = new NotifyIcon
                      {
                          Icon = System.Drawing.SystemIcons.Application,
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

        _notifyIcon.Text = text;
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