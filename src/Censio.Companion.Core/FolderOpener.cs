using Microsoft.Extensions.Logging;

namespace Censio.Companion.Core;

/// <summary>
/// Creates a folder if needed and opens it in the file manager.
/// </summary>
public sealed class FolderOpener
{
    #region Fields

    /// <summary>
    /// The action that opens a folder.
    /// </summary>
    private readonly Action<string> _launch;

    /// <summary>
    /// The logger.
    /// </summary>
    private readonly ILogger _logger;

    #endregion // Fields

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="FolderOpener" /> class.
    /// </summary>
    /// <param name="launch">The action that opens a folder</param>
    /// <param name="logger">The logger</param>
    public FolderOpener(Action<string> launch, ILogger logger)
    {
        _launch = launch;
        _logger = logger;
    }

    #endregion // Constructor

    #region Methods

    /// <summary>
    /// Opens a folder, creating it if it does not exist.
    /// </summary>
    /// <param name="folder">The folder</param>
    /// <param name="displayName">The name of the folder in the log</param>
    public void Open(string folder, string displayName)
    {
        throw new NotImplementedException();
    }

    #endregion // Methods
}