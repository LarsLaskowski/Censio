namespace Censio.Companion.Core;

/// <summary>
/// Forms and parses the names of the log files.
/// </summary>
public static class LogFileNames
{
    #region Methods

    /// <summary>
    /// Forms the name of a log file.
    /// </summary>
    /// <param name="date">The local date</param>
    /// <param name="sequence">The number of the file of the day, starting at 1</param>
    /// <returns>The file name</returns>
    public static string Format(DateOnly date, int sequence)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Parses the name of a log file.
    /// </summary>
    /// <param name="fileName">The file name</param>
    /// <param name="date">The local date</param>
    /// <param name="sequence">The number of the file of the day</param>
    /// <returns><see langword="true" /> if the name is exactly the name of a log file</returns>
    public static bool TryParse(string fileName, out DateOnly date, out int sequence)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Selects the log files to delete, keeping the newest ones.
    /// </summary>
    /// <param name="fileNames">The file names</param>
    /// <param name="keep">The number of log files to keep</param>
    /// <returns>The names to delete</returns>
    public static IReadOnlyList<string> SelectForDeletion(IEnumerable<string> fileNames, int keep)
    {
        throw new NotImplementedException();
    }

    #endregion // Methods
}