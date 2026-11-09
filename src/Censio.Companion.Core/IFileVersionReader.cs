namespace Censio.Companion.Core;

/// <summary>
/// Reads the file version of a program file.
/// </summary>
public interface IFileVersionReader
{
    #region Methods

    /// <summary>
    /// Reads the file version.
    /// </summary>
    /// <param name="filePath">The path of the file</param>
    /// <returns>The file version, or <c>null</c> if it cannot be read</returns>
    Version? ReadVersion(string filePath);

    #endregion // Methods
}