namespace Censio.GameData;

/// <summary>
/// Holds the output lines of an installation survey and whether the sample reads succeeded.
/// </summary>
public sealed class RdaSurveyReport
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="RdaSurveyReport" /> class.
    /// </summary>
    /// <param name="lines">The output lines</param>
    /// <param name="assetRead">Whether the asset sample was read and recognized</param>
    /// <param name="iconRead">Whether the icon sample was read and recognized</param>
    public RdaSurveyReport(IReadOnlyList<string> lines, bool assetRead, bool iconRead)
    {
        Lines = lines;
        AssetRead = assetRead;
        IconRead = iconRead;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the output lines.
    /// </summary>
    public IReadOnlyList<string> Lines { get; }

    /// <summary>
    /// Gets a value indicating whether the asset sample was read and recognized as XML.
    /// </summary>
    public bool AssetRead { get; }

    /// <summary>
    /// Gets a value indicating whether the icon sample was read and recognized as DDS.
    /// </summary>
    public bool IconRead { get; }

    #endregion // Properties
}