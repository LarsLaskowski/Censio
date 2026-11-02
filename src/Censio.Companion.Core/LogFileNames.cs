using System.Globalization;

namespace Censio.Companion.Core;

/// <summary>
/// Forms and parses the names of the log files.
/// </summary>
public static class LogFileNames
{
    #region Constants

    /// <summary>
    /// The start of every log file name.
    /// </summary>
    private const string Prefix = "companion-";

    /// <summary>
    /// The end of every log file name.
    /// </summary>
    private const string Extension = ".log";

    /// <summary>
    /// The length of the date in a log file name.
    /// </summary>
    private const int DateLength = 8;

    /// <summary>
    /// The highest number of a log file of a day.
    /// </summary>
    private const int MaxSequence = 9999;

    #endregion // Constants

    #region Methods

    /// <summary>
    /// Forms the name of a log file.
    /// </summary>
    /// <param name="date">The local date</param>
    /// <param name="sequence">The number of the file of the day, starting at 1</param>
    /// <returns>The file name</returns>
    public static string Format(DateOnly date, int sequence)
    {
        var day = date.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

        return sequence <= 1
                   ? $"{Prefix}{day}{Extension}"
                   : $"{Prefix}{day}-{sequence.ToString(CultureInfo.InvariantCulture)}{Extension}";
    }

    /// <summary>
    /// Parses the name of a log file.
    /// </summary>
    /// <param name="fileName">The file name</param>
    /// <param name="date">The local date</param>
    /// <param name="sequence">The number of the file of the day</param>
    /// <returns><see langword="true" /> if the name is exactly the name of a log file</returns>
    public static bool TryParse(string fileName, out DateOnly date, out int sequence)
    {
        date = default;
        sequence = 0;

        if (fileName.All(char.IsAscii) == false
            || fileName.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) == false
            || fileName.EndsWith(Extension, StringComparison.OrdinalIgnoreCase) == false
            || fileName.Length < Prefix.Length + DateLength + Extension.Length)
        {
            return false;
        }

        var middle = fileName.AsSpan(Prefix.Length, fileName.Length - Prefix.Length - Extension.Length);
        var digits = middle[..DateLength];
        var rest = middle[DateLength..];

        if (IsDigits(digits) == false
            || DateOnly.TryParseExact(digits, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate) == false)
        {
            return false;
        }

        var parsedSequence = 1;

        if (rest.IsEmpty == false)
        {
            if (rest[0] != '-' || IsDigits(rest[1..]) == false || int.TryParse(rest[1..], NumberStyles.None, CultureInfo.InvariantCulture, out parsedSequence) == false)
            {
                return false;
            }

            if (parsedSequence > MaxSequence)
            {
                return false;
            }
        }

        if (string.Equals(Format(parsedDate, parsedSequence), fileName, StringComparison.OrdinalIgnoreCase) == false)
        {
            return false;
        }

        date = parsedDate;
        sequence = parsedSequence;

        return true;
    }

    /// <summary>
    /// Selects the log files to delete, keeping the newest ones.
    /// </summary>
    /// <param name="fileNames">The file names</param>
    /// <param name="keep">The number of log files to keep</param>
    /// <returns>The names to delete</returns>
    public static IReadOnlyList<string> SelectForDeletion(IEnumerable<string> fileNames, int keep)
    {
        var logFiles = new List<(string Name, DateOnly Date, int Sequence)>();

        foreach (var name in fileNames)
        {
            if (TryParse(name, out var date, out var sequence))
            {
                logFiles.Add((name, date, sequence));
            }
        }

        return logFiles.OrderByDescending(file => file.Date)
                       .ThenByDescending(file => file.Sequence)
                       .Skip(keep)
                       .Select(file => file.Name)
                       .ToList();
    }

    /// <summary>
    /// Determines whether a text consists of the digits 0 to 9 only.
    /// </summary>
    /// <param name="text">The text</param>
    /// <returns><see langword="true" /> if the text is not empty and holds only ASCII digits</returns>
    private static bool IsDigits(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
        {
            return false;
        }

        foreach (var character in text)
        {
            if (character is < '0' or > '9')
            {
                return false;
            }
        }

        return true;
    }

    #endregion // Methods
}