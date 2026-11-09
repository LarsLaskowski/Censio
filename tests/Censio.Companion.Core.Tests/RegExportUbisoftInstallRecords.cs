using System.Text.RegularExpressions;

namespace Censio.Companion.Core.Tests;

/// <summary>
/// Ubisoft Connect install records read from a <c>reg export</c> file: the encoding is taken from the byte order mark,
/// each subkey of <c>…\Ubisoft\Launcher\Installs</c> gives one record, named by the subkey, whose install folder is its
/// <c>"InstallDir"="…"</c> string with <c>\\</c> and <c>\"</c> unescaped; other values are ignored.
/// </summary>
public sealed partial class RegExportUbisoftInstallRecords : IUbisoftInstallRecords
{
    #region Fields

    /// <summary>
    /// The path of the export file.
    /// </summary>
    private readonly string _path;

    #endregion // Fields

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="RegExportUbisoftInstallRecords" /> class.
    /// </summary>
    /// <param name="path">The path of the export file</param>
    public RegExportUbisoftInstallRecords(string path)
    {
        _path = path;
    }

    #endregion // Constructor

    #region Methods

    /// <summary>
    /// Matches the header of a subkey of the install records key and captures its name.
    /// </summary>
    /// <returns>The expression</returns>
    [GeneratedRegex(@"^\[HKEY_LOCAL_MACHINE\\SOFTWARE\\(?:WOW6432Node\\)?Ubisoft\\Launcher\\Installs\\(?<name>[^\\\]]+)\]$", RegexOptions.CultureInvariant)]
    private static partial Regex RecordHeader();

    /// <summary>
    /// Matches an <c>InstallDir</c> string value and captures its escaped text.
    /// </summary>
    /// <returns>The expression</returns>
    [GeneratedRegex("""^"InstallDir"="(?<value>(?:[^"\\]|\\.)*)"$""", RegexOptions.CultureInvariant)]
    private static partial Regex InstallDirValue();

    /// <summary>
    /// Matches an escaped character of a string value.
    /// </summary>
    /// <returns>The expression</returns>
    [GeneratedRegex(@"\\(.)", RegexOptions.CultureInvariant)]
    private static partial Regex Escape();

    #endregion // Methods

    #region IUbisoftInstallRecords

    /// <inheritdoc />
    public IReadOnlyList<UbisoftInstallRecord> Read()
    {
        var records = new List<UbisoftInstallRecord>();
        string? name = null;
        string? installDir = null;

        foreach (var rawLine in File.ReadAllLines(_path))
        {
            var line = rawLine.TrimEnd('\r');

            if (line.StartsWith('['))
            {
                AddRecord(records, name, installDir);

                var header = RecordHeader().Match(line);

                name = header.Success ? header.Groups["name"].Value : null;
                installDir = null;

                continue;
            }

            var value = InstallDirValue().Match(line);

            if (name is not null && value.Success)
            {
                installDir = Escape().Replace(value.Groups["value"].Value, "$1");
            }
        }

        AddRecord(records, name, installDir);

        return records;
    }

    /// <summary>
    /// Adds the record of the subkey read so far, if the subkey is one of the install records.
    /// </summary>
    /// <param name="records">The records</param>
    /// <param name="name">The name of the subkey, or <see langword="null" /> if it is no install record</param>
    /// <param name="installDir">The install folder, or <see langword="null" /> if none was read</param>
    private static void AddRecord(List<UbisoftInstallRecord> records, string? name, string? installDir)
    {
        if (name is not null)
        {
            records.Add(new UbisoftInstallRecord
                        {
                            Name = name,
                            InstallDir = installDir
                        });
        }
    }

    #endregion // IUbisoftInstallRecords
}