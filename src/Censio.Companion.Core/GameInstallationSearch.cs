using System.Diagnostics.CodeAnalysis;
using System.Security;

using Microsoft.Extensions.Logging;

namespace Censio.Companion.Core;

/// <summary>
/// Searches the game installation.
/// </summary>
public sealed class GameInstallationSearch
{
    #region Fields

    /// <summary>
    /// The install records of Ubisoft Connect.
    /// </summary>
    private readonly IUbisoftInstallRecords _records;

    /// <summary>
    /// The reader of file versions.
    /// </summary>
    private readonly IFileVersionReader _versions;

    /// <summary>
    /// The default games folder of Ubisoft Connect.
    /// </summary>
    private readonly string _defaultGamesFolder;

    /// <summary>
    /// The logger.
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// The status.
    /// </summary>
    private volatile GameInstallationStatus _status;

    #endregion // Fields

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="GameInstallationSearch" /> class.
    /// </summary>
    /// <param name="records">The install records of Ubisoft Connect</param>
    /// <param name="versions">The reader of file versions</param>
    /// <param name="defaultGamesFolder">The default games folder of Ubisoft Connect</param>
    /// <param name="logger">The logger</param>
    public GameInstallationSearch(IUbisoftInstallRecords records, IFileVersionReader versions, string defaultGamesFolder, ILogger logger)
    {
        _records = records;
        _versions = versions;
        _defaultGamesFolder = defaultGamesFolder;
        _logger = logger;
        _status = GameInstallationStatus.Searching;
    }

    #endregion // Constructor

    #region Properties

    /// <summary>
    /// Gets the status; <see cref="GameInstallationStatus.Searching" /> until <see cref="Run" /> returns.
    /// </summary>
    public GameInstallationStatus Status => _status;

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Runs the search.
    /// </summary>
    /// <param name="settingsFile">The path of the settings file</param>
    /// <returns>The status</returns>
    public GameInstallationStatus Run(string settingsFile)
    {
        GameInstallationStatus status;

        try
        {
            status = Search(settingsFile);
        }
        catch (Exception exception)
        {
            CompanionLog.GameInstallationNotFound(_logger, exception);
            status = new GameInstallationStatus
                     {
                         Kind = GameInstallationStatusKind.NotFound
                     };
        }

        _status = status;

        return status;
    }

    /// <summary>
    /// Determines whether the folder of an install record can be used.
    /// </summary>
    /// <param name="installDir">The folder as written, if any</param>
    /// <returns><c>true</c> if it is a local full path of an installation</returns>
    private static bool IsUsableRecordFolder([NotNullWhen(true)] string? installDir)
    {
        return installDir is not null
               && Path.IsPathFullyQualified(installDir)
               && GameInstallation.IsNetworkOrDevicePath(installDir) == false
               && GameInstallation.IsInstallation(installDir);
    }

    /// <summary>
    /// Determines whether a name consists of ASCII digits only.
    /// </summary>
    /// <param name="name">The name</param>
    /// <returns><c>true</c> if the name is not empty and holds digits only</returns>
    private static bool IsDigits(string name)
    {
        return name.Length > 0 && name.All(character => character is >= '0' and <= '9');
    }

    /// <summary>
    /// Searches the settings file, the install records and the default games folder in turn.
    /// </summary>
    /// <param name="settingsFile">The path of the settings file</param>
    /// <returns>The status</returns>
    private GameInstallationStatus Search(string settingsFile)
    {
        var settings = CompanionSettings.Read(settingsFile);

        if (settings.Kind == CompanionSettingsResultKind.Invalid)
        {
            CompanionLog.GameSettingsFileInvalid(_logger, settingsFile, settings.Problem ?? string.Empty, settings.Exception);

            return new GameInstallationStatus
                   {
                       Kind = GameInstallationStatusKind.SettingsFileInvalid
                   };
        }

        if (settings.Kind == CompanionSettingsResultKind.GameFolder)
        {
            return SearchSettingsFolder(settings.GameFolder ?? string.Empty);
        }

        var folder = FindInRecords() ?? FindInDefaultFolder();

        if (folder is null)
        {
            CompanionLog.GameInstallationNotFound(_logger, null);

            return new GameInstallationStatus
                   {
                       Kind = GameInstallationStatusKind.NotFound
                   };
        }

        return Found(GameInstallationSource.UbisoftConnect, folder);
    }

    /// <summary>
    /// Checks the game folder named in the settings file.
    /// </summary>
    /// <param name="folder">The folder as written</param>
    /// <returns>The status</returns>
    private GameInstallationStatus SearchSettingsFolder(string folder)
    {
        if (Path.IsPathFullyQualified(folder) == false)
        {
            CompanionLog.GameSettingsFolderNotFullPath(_logger, folder);

            return new GameInstallationStatus
                   {
                       Kind = GameInstallationStatusKind.SettingsFolderNotFullPath
                   };
        }

        if (Directory.Exists(folder) == false)
        {
            CompanionLog.GameSettingsFolderMissing(_logger, folder);

            return new GameInstallationStatus
                   {
                       Kind = GameInstallationStatusKind.SettingsFolderNotFound
                   };
        }

        if (GameInstallation.IsInstallation(folder) == false)
        {
            CompanionLog.GameSettingsFolderNotInstallation(_logger, folder);

            return new GameInstallationStatus
                   {
                       Kind = GameInstallationStatusKind.SettingsFolderNotInstallation
                   };
        }

        return Found(GameInstallationSource.SettingsFile, folder);
    }

    /// <summary>
    /// Finds the first installation named by an install record.
    /// </summary>
    /// <returns>The folder as written, or <c>null</c> if there is none</returns>
    private string? FindInRecords()
    {
        IReadOnlyList<UbisoftInstallRecord> records;

        try
        {
            records = _records.Read();
        }
        catch (Exception)
        {
            return null;
        }

        var numeric = records.Where(record => IsDigits(record.Name))
                             .OrderBy(record => record.Name.Length)
                             .ThenBy(record => record.Name, StringComparer.Ordinal);
        var others = records.Where(record => IsDigits(record.Name) == false)
                            .OrderBy(record => record.Name, StringComparer.Ordinal);

        return numeric.Concat(others)
                      .Select(record => record.InstallDir)
                      .FirstOrDefault(IsUsableRecordFolder);
    }

    /// <summary>
    /// Finds the first installation in the default games folder.
    /// </summary>
    /// <returns>The folder, or <c>null</c> if there is none</returns>
    private string? FindInDefaultFolder()
    {
        if (Path.IsPathFullyQualified(_defaultGamesFolder) == false)
        {
            return null;
        }

        try
        {
            return Directory.EnumerateDirectories(_defaultGamesFolder)
                            .Select(folder => Path.Combine(_defaultGamesFolder, Path.GetFileName(folder)))
                            .OrderBy(folder => folder, StringComparer.OrdinalIgnoreCase)
                            .FirstOrDefault(GameInstallation.IsInstallation);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException)
        {
            return null;
        }
    }

    /// <summary>
    /// Builds the status of a found installation and logs it.
    /// </summary>
    /// <param name="source">The source</param>
    /// <param name="folder">The folder as written</param>
    /// <returns>The status</returns>
    private GameInstallationStatus Found(GameInstallationSource source, string folder)
    {
        string? build;

        try
        {
            build = GameInstallation.FormatBuild(_versions.ReadVersion(GameInstallation.ProgramFilePath(folder)));
        }
        catch (Exception)
        {
            build = null;
        }

        CompanionLog.GameInstallationFound(_logger, source == GameInstallationSource.UbisoftConnect ? "Ubisoft Connect" : "settings file", build ?? "unknown", folder);

        return new GameInstallationStatus
               {
                   Kind = GameInstallationStatusKind.Found,
                   Source = source,
                   Folder = folder,
                   Build = build
               };
    }

    #endregion // Methods
}