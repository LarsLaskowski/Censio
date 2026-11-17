namespace Censio.Server;

/// <summary>
/// The result of a chart read.
/// </summary>
public sealed class TrendRead
{
    #region Properties

    /// <summary>
    /// Gets the view, or <see langword="null"/> when the read failed.
    /// </summary>
    public required TrendView? View { get; init; }

    /// <summary>
    /// Gets a value indicating whether the read failed.
    /// </summary>
    public required bool IsFailed { get; init; }

    #endregion // Properties
}