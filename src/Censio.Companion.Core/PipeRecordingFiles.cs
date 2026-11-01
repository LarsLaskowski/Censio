using Censio.Pipe;

namespace Censio.Companion.Core;

/// <summary>
/// Creates the files of the recordings in the recordings folder.
/// </summary>
public sealed class PipeRecordingFiles
{
    #region Constants

    /// <summary>
    /// The highest sequence number tried for the file name of one second.
    /// </summary>
    private const int MaxSequence = 1000;

    #endregion // Constants

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
        return Path.Combine(localApplicationData, "Censio", "Recordings");
    }

    /// <summary>
    /// Creates the file of a recording.
    /// </summary>
    /// <param name="started">The time the recording started</param>
    /// <returns>The stream of the new file</returns>
    /// <exception cref="InvalidOperationException">The recordings folder is not a full path</exception>
    /// <exception cref="IOException">The file cannot be created</exception>
    public Stream Create(DateTimeOffset started)
    {
        if (Path.IsPathFullyQualified(_folder) == false)
        {
            throw new InvalidOperationException("The recordings folder is not a full path.");
        }

        Directory.CreateDirectory(_folder);

        for (var sequence = 1; sequence <= MaxSequence; sequence++)
        {
            var path = Path.Combine(_folder, PipeRecordingFormat.FormatFileName(started, sequence));

            try
            {
                return new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            }
            catch (IOException) when (File.Exists(path))
            {
                // The name is taken: try the next one
            }
        }

        throw new IOException("No free recording file name is left for this second.");
    }

    #endregion // Methods
}