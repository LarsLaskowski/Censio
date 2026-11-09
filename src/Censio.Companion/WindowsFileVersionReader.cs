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
        throw new NotImplementedException();
    }

    #endregion // IFileVersionReader
}