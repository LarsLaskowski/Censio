using System.Globalization;

namespace Censio.Server;

/// <summary>
/// The number and time formats of the pages.
/// </summary>
public static class PageFormat
{
    #region Constants

    /// <summary>
    /// The text of a value the tick holds as no value.
    /// </summary>
    private const string NoValue = "\u2013";

    /// <summary>
    /// The largest offset in minutes, in either direction, that counts as an offset.
    /// </summary>
    private const int MaxOffsetMinutes = 840;

    /// <summary>
    /// The format of a time.
    /// </summary>
    private const string TimeFormat = "yyyy-MM-dd HH:mm";

    #endregion // Constants

    #region Methods

    /// <summary>
    /// Formats a rate.
    /// </summary>
    /// <param name="value">The value</param>
    /// <param name="language">The language</param>
    /// <returns>The text</returns>
    public static string Rate(float? value, UiLanguage language)
    {
        return value is { } rate
                   ? Localize(Group(Round(rate)), language)
                   : NoValue;
    }

    /// <summary>
    /// Formats a delta, with its sign.
    /// </summary>
    /// <param name="value">The value</param>
    /// <param name="language">The language</param>
    /// <returns>The text</returns>
    public static string Delta(float? value, UiLanguage language)
    {
        if (value is not { } delta)
        {
            return NoValue;
        }

        var rounded = Round(delta);

        return Localize(rounded > 0 ? "+" + Group(rounded) : Group(rounded), language);
    }

    /// <summary>
    /// Formats a percentage.
    /// </summary>
    /// <param name="value">The value</param>
    /// <param name="language">The language</param>
    /// <returns>The text</returns>
    public static string Percent(float? value, UiLanguage language)
    {
        if (value is not { } percent)
        {
            return NoValue;
        }

        var text = Math.Round((double)percent, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture);

        return language == UiLanguage.German
                   ? text + " %"
                   : text + "%";
    }

    /// <summary>
    /// Formats a count.
    /// </summary>
    /// <param name="value">The value</param>
    /// <returns>The text</returns>
    public static string Count(long? value)
    {
        return value?.ToString(CultureInfo.InvariantCulture) ?? NoValue;
    }

    /// <summary>
    /// Formats an instant at the browser's UTC offset; an offset outside -840 to 840 minutes, or none, shows UTC.
    /// </summary>
    /// <param name="value">The instant</param>
    /// <param name="offsetMinutes">The offset in minutes, or <see langword="null"/></param>
    /// <returns>The text</returns>
    public static string Time(DateTimeOffset value, int? offsetMinutes)
    {
        if (offsetMinutes is { } offset && offset is >= -MaxOffsetMinutes and <= MaxOffsetMinutes)
        {
            return value.ToOffset(TimeSpan.FromMinutes(offset)).ToString(TimeFormat, CultureInfo.InvariantCulture);
        }

        return value.ToUniversalTime().ToString(TimeFormat, CultureInfo.InvariantCulture) + " UTC";
    }

    /// <summary>
    /// Rounds a value to two decimals, midpoints away from zero; a rounded negative zero is zero.
    /// </summary>
    /// <param name="value">The value</param>
    /// <returns>The rounded value</returns>
    internal static double Round(float value)
    {
        var rounded = Math.Round((double)value, 2, MidpointRounding.AwayFromZero);

        return rounded == 0
                   ? 0
                   : rounded;
    }

    /// <summary>
    /// Formats a value with two decimals and thousands separators of the invariant culture.
    /// </summary>
    /// <param name="value">The value</param>
    /// <returns>The text</returns>
    private static string Group(double value)
    {
        return value.ToString("N2", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Swaps the separators of an invariant number text for German ones.
    /// </summary>
    /// <param name="text">The text</param>
    /// <param name="language">The language</param>
    /// <returns>The text in the language</returns>
    private static string Localize(string text, UiLanguage language)
    {
        return language == UiLanguage.German
                   ? text.Replace(',', '\u0001').Replace('.', ',').Replace('\u0001', '.')
                   : text;
    }

    #endregion // Methods
}