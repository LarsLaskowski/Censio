using System.Diagnostics;
using System.IO;
using System.Security;

using Censio.Companion.Core;

namespace Censio.Companion;

/// <summary>
/// Reads the file version of a program file through Windows.
/// </summary>
internal sealed class WindowsFileVersionReader : IFileVersionReader
{
    #region IFileVersionReader

    /// <inheritdoc />
    public Version? ReadVersion(string filePath)
    {
        try
        {
            var info = FileVersionInfo.GetVersionInfo(filePath);
            var version = new Version(info.FileMajorPart, info.FileMinorPart, info.FileBuildPart, info.FilePrivatePart);

            return version == new Version(0, 0, 0, 0) ? null : version;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException or ArgumentException)
        {
            return null;
        }
    }

    #endregion // IFileVersionReader
}