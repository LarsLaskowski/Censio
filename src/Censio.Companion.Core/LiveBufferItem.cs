namespace Censio.Companion.Core;

/// <summary>
/// A message of the live buffer as the buffer's index holds it: stored in a file, waiting for its write or kept in
/// memory.
/// </summary>
internal sealed class LiveBufferItem
{
    #region Properties

    /// <summary>
    /// Gets the identity of the message.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Gets the time the message was taken into the buffer.
    /// </summary>
    public required DateTimeOffset TakenAt { get; init; }

    /// <summary>
    /// Gets the length of the JSON in bytes.
    /// </summary>
    public required int Length { get; init; }

    /// <summary>
    /// Gets or sets the sequence of the file, or -1 while the message has no file.
    /// </summary>
    public long Sequence { get; set; } = -1;

    /// <summary>
    /// Gets or sets the JSON while the message waits for its write or is kept in memory, or <see langword="null" /> once it is
    /// stored in its file.
    /// </summary>
    public byte[]? Json { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the message is kept in memory only.
    /// </summary>
    public bool IsMemory { get; set; }

    /// <summary>
    /// Gets a value indicating whether the message waits for its write.
    /// </summary>
    public bool IsPending => Json is not null && IsMemory == false;

    #endregion // Properties
}