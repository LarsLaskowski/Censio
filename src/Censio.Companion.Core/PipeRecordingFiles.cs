namespace Censio.Companion.Core;

/// <summary>
/// Creates the files of the recordings in the recordings folder.
/// </summary>
public sealed class PipeRecordingFiles
{
    #region Fields

    /// <summary>
    /// The recordings folder.
    /// </summary>
    private readonly string _folder;

    #endregion // Fields

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="PipeRecordingFiles" /> class.
    /// </summary>
    /// <param name="folder">The recordings folder</param>
    public PipeRecordingFiles(string folder)
    {
        _folder = folder;
    }

    #endregion // Constructor

    #region Methods

    /// <summary>
    /// Gets the default recordings folder.
    /// </summary>
    /// <param name="localApplicationData">The local application data folder</param>
    /// <returns>The recordings folder</returns>
    public static string DefaultFolder(string localApplicationData)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Creates the file of a recording.
    /// </summary>
    /// <param name="started">The time the recording started</param>
    /// <returns>The stream of the new file</returns>
    public Stream Create(DateTimeOffset started)
    {
        throw new NotImplementedException();
    }

    #endregion // Methods
}