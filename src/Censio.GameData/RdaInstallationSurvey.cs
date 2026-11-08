namespace Censio.GameData;

/// <summary>
/// Surveys the RDA archives of a game installation without writing and without reporting entry content.
/// </summary>
public static class RdaInstallationSurvey
{
    #region Methods

    /// <summary>
    /// Opens every archive under the installation's <c>maindata</c> folder and reports what they hold.
    /// </summary>
    /// <param name="installationFolder">The installation folder</param>
    /// <param name="limits">The limits to read within</param>
    /// <returns>The report</returns>
    public static RdaSurveyReport Run(string installationFolder, RdaLimits limits)
    {
        throw new NotImplementedException();
    }

    #endregion // Methods
}