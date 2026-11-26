namespace Censio.Companion.Core;

/// <summary>
/// The kind of the result of writing the settings file.
/// </summary>
public enum CompanionSettingsWriteKind
{
    /// <summary>
    /// The file was written.
    /// </summary>
    Saved,

    /// <summary>
    /// The file was changed since it was read.
    /// </summary>
    ChangedOutside,

    /// <summary>
    /// The file could not be written.
    /// </summary>
    Failed
}