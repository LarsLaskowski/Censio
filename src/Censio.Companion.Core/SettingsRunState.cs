namespace Censio.Companion.Core;

/// <summary>
/// What the running Companion does that the settings window warns about.
/// </summary>
public readonly record struct SettingsRunState
{
    #region Properties

    /// <summary>
    /// Gets a value indicating whether a replay is running.
    /// </summary>
    public bool IsReplay { get; init; }

    /// <summary>
    /// Gets a value indicating whether a recording is running.
    /// </summary>
    public bool IsRecording { get; init; }

    /// <summary>
    /// Gets a value indicating whether the buffer is kept in memory only.
    /// </summary>
    public bool IsBufferMemoryOnly { get; init; }

    #endregion // Properties
}