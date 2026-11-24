namespace Censio.Companion.Core;

/// <summary>
/// A written catalog message: its id and its UTF-8 JSON bytes.
/// </summary>
public readonly record struct LiveCatalogMessage
{
    #region Properties

    /// <summary>
    /// Gets the id of the message.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Gets the UTF-8 JSON bytes of the message.
    /// </summary>
    public required ReadOnlyMemory<byte> Utf8Json { get; init; }

    #endregion // Properties
}