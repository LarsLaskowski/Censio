namespace Censio.Companion.Core;

/// <summary>
/// A removed profile whose history the server has to settle: a deletion, or a merge into a target profile.
/// </summary>
public sealed class ProfileRemoval
{
    #region Properties

    /// <summary>
    /// Gets the identity of the removed profile.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Gets the identity of the profile the history is merged into, or <see langword="null" /> for a deletion.
    /// </summary>
    public Guid? Target { get; init; }

    /// <summary>
    /// Gets a value indicating whether the server confirmed the removal.
    /// </summary>
    public required bool IsConfirmed { get; init; }

    #endregion // Properties
}