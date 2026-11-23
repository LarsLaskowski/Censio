namespace Censio.Companion.Core;

/// <summary>
/// A message of the live buffer.
/// </summary>
public sealed class LiveBufferEntry
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
    /// Gets the UTF-8 JSON of the message.
    /// </summary>
    public required ReadOnlyMemory<byte> Utf8Json { get; init; }

    #endregion // Properties
}