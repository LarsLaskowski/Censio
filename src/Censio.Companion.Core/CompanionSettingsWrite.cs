namespace Censio.Companion.Core;

/// <summary>
/// The result of writing the settings file.
/// </summary>
public sealed record CompanionSettingsWrite
{
    #region Properties

    /// <summary>
    /// Gets the kind of the result.
    /// </summary>
    public required CompanionSettingsWriteKind Kind { get; init; }

    /// <summary>
    /// Gets the exception of a failed write.
    /// </summary>
    public Exception? Exception { get; init; }

    #endregion // Properties
}