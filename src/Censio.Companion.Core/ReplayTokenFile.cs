namespace Censio.Companion.Core;

/// <summary>
/// Reads the token file of a replay that is sent to a server.
/// </summary>
public static class ReplayTokenFile
{
    #region Constants

    /// <summary>
    /// The largest size of a token file in bytes.
    /// </summary>
    public const int MaxFileBytes = 65536;

    #endregion // Constants

    #region Methods

    /// <summary>
    /// Reads the token from a file.
    /// </summary>
    /// <param name="path">The path of the token file</param>
    /// <returns>The result of reading the file</returns>
    public static ReplayTokenFileResult ReadFile(string path)
    {
#pragma warning disable RH2003, S1172 // Skeleton: removed with the implementation
        throw new NotImplementedException();
#pragma warning restore RH2003, S1172 // Skeleton: removed with the implementation
    }

    #endregion // Methods
}