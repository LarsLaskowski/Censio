namespace Censio.Server;

/// <summary>
/// The number and time formats of the pages.
/// </summary>
public static class PageFormat
{
    #region Methods

    /// <summary>
    /// Formats a rate.
    /// </summary>
    /// <param name="value">The value</param>
    /// <param name="language">The language</param>
    /// <returns>The text</returns>
#pragma warning disable S1172, RH2003 // Skeleton: removed with the implementation
    public static string Rate(float? value, UiLanguage language)
    {
        throw new NotImplementedException();
    }
#pragma warning restore S1172, RH2003

    /// <summary>
    /// Formats a delta, with its sign.
    /// </summary>
    /// <param name="value">The value</param>
    /// <param name="language">The language</param>
    /// <returns>The text</returns>
#pragma warning disable S1172, RH2003 // Skeleton: removed with the implementation
    public static string Delta(float? value, UiLanguage language)
    {
        throw new NotImplementedException();
    }
#pragma warning restore S1172, RH2003

    /// <summary>
    /// Formats a percentage.
    /// </summary>
    /// <param name="value">The value</param>
    /// <param name="language">The language</param>
    /// <returns>The text</returns>
#pragma warning disable S1172, RH2003 // Skeleton: removed with the implementation
    public static string Percent(float? value, UiLanguage language)
    {
        throw new NotImplementedException();
    }
#pragma warning restore S1172, RH2003

    /// <summary>
    /// Formats a count.
    /// </summary>
    /// <param name="value">The value</param>
    /// <returns>The text</returns>
#pragma warning disable S1172, RH2003 // Skeleton: removed with the implementation
    public static string Count(int? value)
    {
        throw new NotImplementedException();
    }
#pragma warning restore S1172, RH2003

    /// <summary>
    /// Formats an instant at the browser's UTC offset; an offset outside -840 to 840 minutes, or none, shows UTC.
    /// </summary>
    /// <param name="value">The instant</param>
    /// <param name="offsetMinutes">The offset in minutes, or <see langword="null"/></param>
    /// <returns>The text</returns>
#pragma warning disable S1172, RH2003 // Skeleton: removed with the implementation
    public static string Time(DateTimeOffset value, int? offsetMinutes)
    {
        throw new NotImplementedException();
    }
#pragma warning restore S1172, RH2003

    #endregion // Methods
}