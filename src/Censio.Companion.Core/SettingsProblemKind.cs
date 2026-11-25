namespace Censio.Companion.Core;

/// <summary>
/// The kind of a problem with a value of the settings window.
/// </summary>
public enum SettingsProblemKind
{
    /// <summary>
    /// The port is outside the allowed range.
    /// </summary>
    PortRange,

    /// <summary>
    /// The server address is missing.
    /// </summary>
    AddressMissing,

    /// <summary>
    /// The server address is not valid.
    /// </summary>
    AddressInvalid,

    /// <summary>
    /// The token is not valid.
    /// </summary>
    TokenInvalid,

    /// <summary>
    /// The buffer hours are outside the allowed range.
    /// </summary>
    HoursRange,

    /// <summary>
    /// The buffer megabytes are outside the allowed range.
    /// </summary>
    MegabytesRange,

    /// <summary>
    /// The game folder is not a full path.
    /// </summary>
    FolderNotFullPath,

    /// <summary>
    /// The game folder does not exist.
    /// </summary>
    FolderMissing,

    /// <summary>
    /// The game folder is not an installation.
    /// </summary>
    FolderNotInstallation,

    /// <summary>
    /// The profile name is blank.
    /// </summary>
    NameBlank,

    /// <summary>
    /// The profile name is too long.
    /// </summary>
    NameTooLong,

    /// <summary>
    /// The profile name holds characters that are not allowed.
    /// </summary>
    NameInvalidCharacters,

    /// <summary>
    /// A profile of that name exists.
    /// </summary>
    NameExists,

    /// <summary>
    /// The profile list is full.
    /// </summary>
    ProfilesFull,

    /// <summary>
    /// The mode of the settings file is no text, or a text other than local or forward.
    /// </summary>
    ModeInvalid,

    /// <summary>
    /// A known key is written twice in the settings file.
    /// </summary>
    WrittenTwice
}