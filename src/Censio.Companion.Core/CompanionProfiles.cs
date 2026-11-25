using Microsoft.Extensions.Logging;

namespace Censio.Companion.Core;

/// <summary>
/// The profiles live data is sent under, and the figures each was used with.
/// </summary>
public sealed class CompanionProfiles
{
    #region Constants

    /// <summary>
    /// The largest size of the profiles file in bytes.
    /// </summary>
    public const int MaxFileLength = 1048576;

    /// <summary>
    /// The largest number of profiles.
    /// </summary>
    public const int MaxProfiles = 1000;

    /// <summary>
    /// The largest number of remembered figures.
    /// </summary>
    public const int MaxFigures = 1000;

    /// <summary>
    /// The longest profile name in characters.
    /// </summary>
    public const int MaxNameLength = 64;

    #endregion // Constants

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="CompanionProfiles" /> class.
    /// </summary>
    /// <param name="profiles">The profiles</param>
    /// <param name="active">The active profile</param>
    /// <param name="isFileInvalid">Whether the profiles file is invalid</param>
    internal CompanionProfiles(IReadOnlyList<CompanionProfile> profiles, CompanionProfile active, bool isFileInvalid)
    {
        Profiles = profiles;
        Active = active;
        IsFileInvalid = isFileInvalid;
    }

    #endregion // Constructor

    #region Events

#pragma warning disable CS0067, S3264 // Skeleton: removed with the implementation
    /// <summary>
    /// Occurs when the active profile changes.
    /// </summary>
    public event EventHandler? ActiveChanged;

#pragma warning restore CS0067, S3264 // Skeleton: removed with the implementation

    #endregion // Events

    #region Properties

    /// <summary>
    /// Gets the profiles.
    /// </summary>
    public IReadOnlyList<CompanionProfile> Profiles { get; }

    /// <summary>
    /// Gets the active profile.
    /// </summary>
    public CompanionProfile Active { get; }

    /// <summary>
    /// Gets a value indicating whether the profiles file is invalid.
    /// </summary>
    public bool IsFileInvalid { get; }

    #endregion // Properties

    #region Methods

#pragma warning disable RH2003 // Skeleton: removed with the implementation
    /// <summary>
    /// Reads the profiles file.
    /// </summary>
    /// <param name="path">The path of the profiles file</param>
    /// <param name="logger">The logger</param>
    /// <returns>The profiles</returns>
    public static CompanionProfiles Load(string path, ILogger logger)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Checks a profile name.
    /// </summary>
    /// <param name="name">The name</param>
    /// <returns>The problem, or <see langword="null" /> when the name is valid</returns>
    public static SettingsProblemKind? CheckName(string name)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Tells whether the Companion sends live data that profiles apply to.
    /// </summary>
    /// <param name="mode">The mode</param>
    /// <param name="setting">The setting</param>
    /// <returns><see langword="true" /> when the figures of a session are observed</returns>
    public static bool IsObserved(CompanionMode mode, LocalServerSetting setting)
    {
        throw new NotImplementedException();
    }

#pragma warning disable S2325 // Skeleton: removed with the implementation
    /// <summary>
    /// Checks the name of a profile to create.
    /// </summary>
    /// <param name="name">The name</param>
    /// <returns>The problem, or <see langword="null" /> when the name is valid</returns>
    public SettingsProblemKind? CheckNewName(string name)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Creates a profile and makes it the active one.
    /// </summary>
    /// <param name="name">The name</param>
    /// <returns>The profile</returns>
    public CompanionProfile Create(string name)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Makes a profile the active one.
    /// </summary>
    /// <param name="id">The identity of the profile</param>
    public void Select(Guid id)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Gets the name of a profile.
    /// </summary>
    /// <param name="id">The identity of the profile</param>
    /// <returns>The name, or <see langword="null" /> when there is no such profile</returns>
    public string? NameOf(Guid id)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Observes the figure of a session start.
    /// </summary>
    /// <param name="figure">The name of the figure</param>
    /// <returns>The notice to show, or <see langword="null" /> when none is due</returns>
    public ProfileNotice? ObserveSessionStart(string figure)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Observes the end of a session.
    /// </summary>
    public void ObserveSessionEnd()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Writes the profiles file when something changed, until cancelled.
    /// </summary>
    /// <param name="cancellationToken">The token that ends the run</param>
    /// <returns>The task</returns>
    public Task RunAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

#pragma warning restore S2325 // Skeleton: removed with the implementation

#pragma warning restore RH2003 // Skeleton: removed with the implementation

    #endregion // Methods
}