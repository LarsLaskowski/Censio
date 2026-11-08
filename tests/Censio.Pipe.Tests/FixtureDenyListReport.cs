using System.Globalization;
using System.Text;

namespace Censio.Pipe.Tests;

/// <summary>
/// The result of a deny-list check: counts, fixture names, line numbers and field names only, never a name or a value.
/// </summary>
public sealed class FixtureDenyListReport
{
    #region Fields

    /// <summary>
    /// The findings.
    /// </summary>
    private readonly List<string> _findings = [];

    #endregion // Fields

    #region Properties

    /// <summary>
    /// Gets the number of fixtures checked.
    /// </summary>
    public int FixtureCount { get; private set; }

    /// <summary>
    /// Gets the number of non-empty names checked.
    /// </summary>
    public long NameCount { get; private set; }

    /// <summary>
    /// Gets the number of non-zero finite values compared with the original.
    /// </summary>
    public long ComparedValueCount { get; private set; }

    /// <summary>
    /// Gets the findings, each naming a fixture or a deny-list line, a line number and a field.
    /// </summary>
    public IReadOnlyList<string> Findings => _findings;

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Counts a fixture.
    /// </summary>
    public void AddFixture()
    {
        FixtureCount++;
    }

    /// <summary>
    /// Counts a name.
    /// </summary>
    public void AddName()
    {
        NameCount++;
    }

    /// <summary>
    /// Counts a compared value.
    /// </summary>
    public void AddComparedValue()
    {
        ComparedValueCount++;
    }

    /// <summary>
    /// Adds a finding.
    /// </summary>
    /// <param name="finding">The finding, without a name or a value</param>
    public void AddFinding(string finding)
    {
        _findings.Add(finding);
    }

    #endregion // Methods

    #region Object

    /// <inheritdoc />
    public override string ToString()
    {
        var text = new StringBuilder();

        text.Append(CultureInfo.InvariantCulture, $"{FixtureCount} fixtures, {NameCount} names, {ComparedValueCount} values compared, {_findings.Count} findings");

        foreach (var finding in _findings)
        {
            text.Append('\n').Append(finding);
        }

        return text.ToString();
    }

    #endregion // Object
}