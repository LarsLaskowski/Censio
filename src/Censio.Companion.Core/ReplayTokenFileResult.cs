namespace Censio.Companion.Core;

/// <summary>
/// The result of reading a token file.
/// </summary>
public sealed class ReplayTokenFileResult
{
    #region Properties

    /// <summary>
    /// Gets the outcome kind.
    /// </summary>
    public required ReplayTokenFileResultKind Kind { get; init; }

    /// <summary>
    /// Gets the token; <c>null</c> unless the kind is <see cref="ReplayTokenFileResultKind.Read" />.
    /// </summary>
    public string? Token { get; init; }

    #endregion // Properties
}