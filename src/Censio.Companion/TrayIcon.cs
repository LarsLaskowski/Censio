#pragma warning disable RH2003 // Skeleton body until the implementation task

using System.Windows.Forms;

namespace Censio.Companion;

/// <summary>
/// The notification area icon with its context menu.
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    #region Fields

    /// <summary>
    /// The product version
    /// </summary>
    private readonly string _version;

    /// <summary>
    /// The action the exit entry runs
    /// </summary>
    private readonly Action _exit;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="TrayIcon" /> class.
    /// </summary>
    /// <param name="version">The product version</param>
    /// <param name="exit">The action the exit entry runs</param>
    public TrayIcon(string version, Action exit)
    {
        _version = version;
        _exit = exit;
    }

    #endregion // Constructors

    #region IDisposable

    /// <inheritdoc />
    public void Dispose()
    {
        throw new NotImplementedException(_version + _exit.Method.Name);
    }

    #endregion // IDisposable
}