namespace Censio.Companion.Core;

/// <summary>
/// The settings file as the settings window reads and writes it.
/// </summary>
public sealed class CompanionSettingsDocument
{
    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="CompanionSettingsDocument" /> class.
    /// </summary>
    /// <param name="values">The values</param>
    /// <param name="fileProblem">The problem of the file, or <see langword="null" /></param>
    /// <param name="fieldProblems">The problems of the fields</param>
    internal CompanionSettingsDocument(CompanionSettingsValues values, string? fileProblem, IReadOnlyList<SettingsProblem> fieldProblems)
    {
        Values = values;
        FileProblem = fileProblem;
        FieldProblems = fieldProblems;
    }

    #endregion // Constructor

    #region Properties

    /// <summary>
    /// Gets the values of the file.
    /// </summary>
    public CompanionSettingsValues Values { get; }

    /// <summary>
    /// Gets the problem of the file, or <see langword="null" /> when it can be read.
    /// </summary>
    public string? FileProblem { get; }

    /// <summary>
    /// Gets the problems of the fields.
    /// </summary>
    public IReadOnlyList<SettingsProblem> FieldProblems { get; }

    #endregion // Properties

    #region Methods

#pragma warning disable RH2003 // Skeleton: removed with the implementation
    /// <summary>
    /// Reads the settings file.
    /// </summary>
    /// <param name="path">The path of the settings file</param>
    /// <returns>The document</returns>
    public static CompanionSettingsDocument Load(string path)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Tells whether saved values need a restart of the Companion.
    /// </summary>
    /// <param name="running">The values the running Companion started with</param>
    /// <param name="saved">The saved values</param>
    /// <returns><see langword="true" /> when a restart is needed</returns>
    public static bool NeedsRestart(CompanionSettingsValues running, CompanionSettingsValues saved)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Writes the values to the settings file.
    /// </summary>
    /// <param name="values">The values</param>
    /// <returns>The result</returns>
#pragma warning disable S2325 // Skeleton: removed with the implementation
    public CompanionSettingsWrite Save(CompanionSettingsValues values)
    {
        throw new NotImplementedException();
    }
#pragma warning restore S2325 // Skeleton: removed with the implementation

#pragma warning restore RH2003 // Skeleton: removed with the implementation

    #endregion // Methods
}