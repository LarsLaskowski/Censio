namespace Censio.Companion.Core;

/// <summary>
/// Describes what makes a folder a game installation.
/// </summary>
public static class GameInstallation
{
    #region Methods

    /// <summary>
    /// Determines whether a folder is a game installation.
    /// </summary>
    /// <param name="folder">The folder</param>
    /// <returns><c>true</c> if the folder is an installation</returns>
    public static bool IsInstallation(string folder)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Determines whether a path is a network or device path.
    /// </summary>
    /// <param name="path">The path</param>
    /// <returns><c>true</c> if the path is a network or device path</returns>
    public static bool IsNetworkOrDevicePath(string path)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Gets the path of the program file of an installation.
    /// </summary>
    /// <param name="folder">The folder of the installation</param>
    /// <returns>The path of the program file</returns>
    public static string ProgramFilePath(string folder)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Gets the default games folder of Ubisoft Connect.
    /// </summary>
    /// <param name="programFilesX86">The Program Files (x86) folder</param>
    /// <returns>The default games folder</returns>
    public static string DefaultUbisoftGamesFolder(string programFilesX86)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Formats the build of a file version.
    /// </summary>
    /// <param name="version">The file version</param>
    /// <returns>The build, or <c>null</c> if it is unknown</returns>
    public static string? FormatBuild(Version? version)
    {
        throw new NotImplementedException();
    }

    #endregion // Methods
}