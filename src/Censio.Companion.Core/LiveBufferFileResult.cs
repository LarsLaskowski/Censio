namespace Censio.Companion.Core;

/// <summary>
/// The result of reading a live buffer file or its header.
/// </summary>
public sealed class LiveBufferFileResult
{
    #region Properties

    /// <summary>
    /// Gets the outcome kind.
    /// </summary>
    public required LiveBufferFileResultKind Kind { get; init; }

    /// <summary>
    /// Gets the entry; <c>null</c> unless the kind is <see cref="LiveBufferFileResultKind.Read" />. After
    /// <see cref="LiveBufferFile.ReadHeader" /> its JSON is empty.
    /// </summary>
    public LiveBufferEntry? Entry { get; init; }

    /// <summary>
    /// Gets the length of the JSON in bytes.
    /// </summary>
    public int Length { get; init; }

    #endregion // Properties
}