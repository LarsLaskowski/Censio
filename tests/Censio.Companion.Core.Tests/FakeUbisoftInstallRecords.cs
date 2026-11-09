namespace Censio.Companion.Core.Tests;

/// <summary>
/// Ubisoft Connect install records that return a fixed list or throw a given exception, and count the reads.
/// </summary>
public sealed class FakeUbisoftInstallRecords : IUbisoftInstallRecords
{
    #region Fields

    /// <summary>
    /// The records every read returns.
    /// </summary>
    private readonly IReadOnlyList<UbisoftInstallRecord> _records;

    #endregion // Fields

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeUbisoftInstallRecords" /> class.
    /// </summary>
    /// <param name="records">The records every read returns, in this order</param>
    public FakeUbisoftInstallRecords(params UbisoftInstallRecord[] records)
    {
        _records = records;
    }

    #endregion // Constructor

    #region Properties

    /// <summary>
    /// Gets or sets the exception a read throws, or <see langword="null" /> to return the records.
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// Gets the number of reads, including failed ones.
    /// </summary>
    public int ReadCount { get; private set; }

    #endregion // Properties

    #region IUbisoftInstallRecords

    /// <inheritdoc />
    public IReadOnlyList<UbisoftInstallRecord> Read()
    {
        ReadCount++;

        if (Exception is not null)
        {
            throw Exception;
        }

        return _records;
    }

    #endregion // IUbisoftInstallRecords
}