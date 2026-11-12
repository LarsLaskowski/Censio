namespace Censio.Contracts;

/// <summary>
/// Reads contract messages strictly and within bounds.
/// </summary>
public static class ContractReader
{
    #region Constants

    /// <summary>
    /// The only contract version this build reads and writes.
    /// </summary>
    public const int CurrentVersion = 1;

    /// <summary>
    /// The largest message in bytes.
    /// </summary>
    public const int MaxMessageBytes = 25165824;

    /// <summary>
    /// The largest binary payload in bytes after Base64 decoding.
    /// </summary>
    public const int MaxBinaryBytes = 17825792;

    /// <summary>
    /// The largest string or property name in UTF-8 bytes.
    /// </summary>
    public const int MaxStringBytes = 1024;

    /// <summary>
    /// The largest island name in UTF-8 bytes.
    /// </summary>
    public const int MaxIslandNameBytes = 765;

    /// <summary>
    /// The most islands in a tick.
    /// </summary>
    public const int MaxIslands = 256;

    /// <summary>
    /// The most entries and map pairs in a tick.
    /// </summary>
    public const int MaxEntriesAndPairs = 524288;

    /// <summary>
    /// The most pairs in a map.
    /// </summary>
    public const int MaxPairsPerMap = 1024;

    /// <summary>
    /// The most supported versions in a refusal.
    /// </summary>
    public const int MaxSupportedVersions = 16;

    /// <summary>
    /// The most missing icons in an acknowledgement.
    /// </summary>
    public const int MaxMissingIcons = 16384;

    #endregion // Constants

    #region Methods

    /// <summary>
    /// Reads a contract message.
    /// </summary>
    /// <param name="utf8Json">The UTF-8 JSON bytes of the message</param>
    /// <returns>The message or the refusal</returns>
#pragma warning disable RH2003 // Skeleton: removed with the implementation
    public static ContractReadResult Read(ReadOnlySpan<byte> utf8Json)
    {
        throw new NotImplementedException();
    }
#pragma warning restore RH2003 // Skeleton: removed with the implementation

    #endregion // Methods
}