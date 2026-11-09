using System.Globalization;

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
        return File.Exists(Path.Combine(folder, "maindata", "config.rda")) && File.Exists(ProgramFilePath(folder));
    }

    /// <summary>
    /// Determines whether a path is a network or device path.
    /// </summary>
    /// <param name="path">The path</param>
    /// <returns><c>true</c> if the path is a network or device path</returns>
    public static bool IsNetworkOrDevicePath(string path)
    {
        return path.Length >= 2 && IsSeparator(path[0]) && (IsSeparator(path[1]) || path[1] == '?');
    }

    /// <summary>
    /// Gets the path of the program file of an installation.
    /// </summary>
    /// <param name="folder">The folder of the installation</param>
    /// <returns>The path of the program file</returns>
    public static string ProgramFilePath(string folder)
    {
        return Path.Combine(folder, "bin", "win64", "anno117.exe");
    }

    /// <summary>
    /// Gets the default games folder of Ubisoft Connect.
    /// </summary>
    /// <param name="programFilesX86">The Program Files (x86) folder</param>
    /// <returns>The default games folder</returns>
    public static string DefaultUbisoftGamesFolder(string programFilesX86)
    {
        return Path.Combine(programFilesX86, "Ubisoft", "Ubisoft Game Launcher", "games");
    }

    /// <summary>
    /// Formats the build of a file version.
    /// </summary>
    /// <param name="version">The file version</param>
    /// <returns>The build, or <c>null</c> if it is unknown</returns>
    public static string? FormatBuild(Version? version)
    {
        if (version is null || (version.Major <= 0 && version.Minor <= 0 && version.Build <= 0 && version.Revision <= 0))
        {
            return null;
        }

        return string.Create(CultureInfo.InvariantCulture, $"{Math.Max(version.Major, 0)}.{Math.Max(version.Minor, 0)}.{Math.Max(version.Build, 0)}.{Math.Max(version.Revision, 0)}");
    }

    /// <summary>
    /// Determines whether a character is a path separator, <c>\</c> and <c>/</c> counted on every system.
    /// </summary>
    /// <param name="character">The character</param>
    /// <returns><c>true</c> if the character is a separator</returns>
    private static bool IsSeparator(char character)
    {
        return character is '\\' or '/';
    }

    #endregion // Methods
}