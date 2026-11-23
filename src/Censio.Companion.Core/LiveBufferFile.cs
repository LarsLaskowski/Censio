namespace Censio.Companion.Core;

/// <summary>
/// The file format of the live buffer.
/// </summary>
public static class LiveBufferFile
{
    #region Constants

    /// <summary>
    /// The length of the header in bytes.
    /// </summary>
    public const int HeaderLength = 68;

    /// <summary>
    /// The format version.
    /// </summary>
    public const int FormatVersion = 1;

    #endregion // Constants

    #region Methods

#pragma warning disable RH2003
    // Skeleton: removed with the implementation
    /// <summary>
    /// Formats an entry as the bytes of a file.
    /// </summary>
    /// <param name="entry">The entry</param>
    /// <returns>The bytes of the file</returns>
    public static byte[] Format(LiveBufferEntry entry)
    {
        throw new NotImplementedException();
    }
#pragma warning restore RH2003

#pragma warning disable RH2003
    // Skeleton: removed with the implementation
    /// <summary>
    /// Reads the header of a file.
    /// </summary>
    /// <param name="header">The first bytes of the file</param>
    /// <param name="fileLength">The length of the file</param>
    /// <returns>The result, with the entry holding an empty JSON</returns>
    public static LiveBufferFileResult ReadHeader(ReadOnlySpan<byte> header, long fileLength)
    {
        throw new NotImplementedException();
    }
#pragma warning restore RH2003

#pragma warning disable RH2003
    // Skeleton: removed with the implementation
    /// <summary>
    /// Reads a whole file.
    /// </summary>
    /// <param name="file">The bytes of the file</param>
    /// <returns>The result</returns>
    public static LiveBufferFileResult Read(ReadOnlySpan<byte> file)
    {
        throw new NotImplementedException();
    }
#pragma warning restore RH2003

#pragma warning disable RH2003
    // Skeleton: removed with the implementation
    /// <summary>
    /// Gets the name of a message file.
    /// </summary>
    /// <param name="sequence">The sequence number</param>
    /// <returns>The file name</returns>
    public static string MessageName(long sequence)
    {
        throw new NotImplementedException();
    }
#pragma warning restore RH2003

#pragma warning disable RH2003
    // Skeleton: removed with the implementation
    /// <summary>
    /// Gets the name of a temporary file.
    /// </summary>
    /// <param name="sequence">The sequence number</param>
    /// <returns>The file name</returns>
    public static string TemporaryName(long sequence)
    {
        throw new NotImplementedException();
    }
#pragma warning restore RH2003

#pragma warning disable RH2003
    // Skeleton: removed with the implementation
    /// <summary>
    /// Tries to parse the name of a message file.
    /// </summary>
    /// <param name="name">The file name</param>
    /// <param name="sequence">The sequence number</param>
    /// <returns><see langword="true" /> when the name is a message name</returns>
    public static bool TryParseMessageName(string name, out long sequence)
    {
        throw new NotImplementedException();
    }
#pragma warning restore RH2003

#pragma warning disable RH2003
    // Skeleton: removed with the implementation
    /// <summary>
    /// Tries to parse the name of a temporary file.
    /// </summary>
    /// <param name="name">The file name</param>
    /// <param name="sequence">The sequence number</param>
    /// <returns><see langword="true" /> when the name is a temporary name</returns>
    public static bool TryParseTemporaryName(string name, out long sequence)
    {
        throw new NotImplementedException();
    }
#pragma warning restore RH2003

    #endregion // Methods
}