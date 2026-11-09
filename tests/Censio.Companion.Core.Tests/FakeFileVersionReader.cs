namespace Censio.Companion.Core.Tests;

/// <summary>
/// A file version reader that returns a version set per file path, compared as full paths, or throws a given
/// exception, and records the paths it is asked for.
/// </summary>
public sealed class FakeFileVersionReader : IFileVersionReader
{
    #region Fields

    /// <summary>
    /// The versions by full file path.
    /// </summary>
    private readonly Dictionary<string, Version?> _versions = new(StringComparer.Ordinal);

    /// <summary>
    /// The paths asked for, in order.
    /// </summary>
    private readonly List<string> _reads = [];

    #endregion // Fields

    #region Properties

    /// <summary>
    /// Gets or sets the exception a read throws, or <see langword="null" /> to return the version.
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// Gets the paths asked for, in order, including failed reads.
    /// </summary>
    public IReadOnlyList<string> Reads => _reads;

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Sets the version of a file.
    /// </summary>
    /// <param name="filePath">The file path</param>
    /// <param name="version">The version, or <see langword="null" /> for a file without a version</param>
    public void Set(string filePath, Version? version)
    {
        _versions[Path.GetFullPath(filePath)] = version;
    }

    #endregion // Methods

    #region IFileVersionReader

    /// <inheritdoc />
    public Version? ReadVersion(string filePath)
    {
        _reads.Add(filePath);

        if (Exception is not null)
        {
            throw Exception;
        }

        return _versions.GetValueOrDefault(Path.GetFullPath(filePath));
    }

    #endregion // IFileVersionReader
}