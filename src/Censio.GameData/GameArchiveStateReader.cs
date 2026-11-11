namespace Censio.GameData;

/// <summary>
/// Lists the archive files below the <c>maindata</c> folder of a game installation with their size and time.
/// </summary>
public static class GameArchiveStateReader
{
    #region Constants

    /// <summary>
    /// The name of the folder that holds the archives.
    /// </summary>
    private const string MainDataFolder = "maindata";

    #endregion // Constants

    #region Methods

    /// <summary>
    /// Reads the state of all archive files below the <c>maindata</c> folder.
    /// </summary>
    /// <param name="installationFolder">The installation folder</param>
    /// <returns>The archives in a stable order</returns>
    /// <exception cref="DirectoryNotFoundException">The installation folder or its <c>maindata</c> folder does not exist</exception>
    /// <exception cref="IOException">A file stands in place of a folder, or a file cannot be read</exception>
    /// <exception cref="UnauthorizedAccessException">A folder below <c>maindata</c> cannot be read</exception>
    public static IReadOnlyList<GameArchiveState> Read(string installationFolder)
    {
        RequireFolder(installationFolder);

        var mainData = Path.Combine(installationFolder, MainDataFolder);

        RequireFolder(mainData);

        var options = new EnumerationOptions
                      {
                          RecurseSubdirectories = true,
                          IgnoreInaccessible = false,
                          AttributesToSkip = FileAttributes.ReparsePoint
                      };

        return Directory.EnumerateFiles(mainData, "*", options)
                        .Where(file => file.EndsWith(".rda", StringComparison.OrdinalIgnoreCase))
                        .Select(file => Describe(mainData, file))
                        .OrderBy(state => state.Path, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(state => state.Path, StringComparer.Ordinal)
                        .ToList();
    }

    /// <summary>
    /// Requires that a path is an existing folder.
    /// </summary>
    /// <param name="folder">The path</param>
    private static void RequireFolder(string folder)
    {
        if (Directory.Exists(folder))
        {
            return;
        }

        if (File.Exists(folder))
        {
            throw new IOException($"'{folder}' is a file, not a folder");
        }

        throw new DirectoryNotFoundException($"The folder '{folder}' does not exist");
    }

    /// <summary>
    /// Describes one archive file.
    /// </summary>
    /// <param name="mainData">The <c>maindata</c> folder</param>
    /// <param name="file">The path of the file</param>
    /// <returns>The state</returns>
    private static GameArchiveState Describe(string mainData, string file)
    {
        var info = new FileInfo(file);

        return new GameArchiveState
               {
                   Path = Path.GetRelativePath(mainData, file),
                   Length = info.Length,
                   LastWriteTimeUtc = info.LastWriteTimeUtc
               };
    }

    #endregion // Methods
}