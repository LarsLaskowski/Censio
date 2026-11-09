using Microsoft.Extensions.Logging;

namespace Censio.Companion.Core;

/// <summary>
/// Searches the game installation.
/// </summary>
public sealed class GameInstallationSearch
{
    #region Fields

    // Skeleton: removed with the implementation
#pragma warning disable S4487

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

#pragma warning restore S4487

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
        Status = GameInstallationStatus.Searching;
    }

    #endregion // Constructor

    #region Properties

    /// <summary>
    /// Gets the status; <see cref="GameInstallationStatus.Searching" /> until <see cref="Run" /> returns.
    /// </summary>
    public GameInstallationStatus Status { get; private set; }

    #endregion // Properties

    #region Methods

    // Skeleton: removed with the implementation
#pragma warning disable S2325
    /// <summary>
    /// Runs the search.
    /// </summary>
    /// <param name="settingsFile">The path of the settings file</param>
    /// <returns>The status</returns>
    public GameInstallationStatus Run(string settingsFile)
    {
        throw new NotImplementedException();
    }

#pragma warning restore S2325

    #endregion // Methods
}