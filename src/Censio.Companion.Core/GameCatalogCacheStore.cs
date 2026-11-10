using System.Buffers;
using System.Security;

using Censio.GameData;

namespace Censio.Companion.Core;

/// <summary>
/// Loads and saves the catalog cache file in the cache folder.
/// </summary>
public sealed class GameCatalogCacheStore
{
    #region Constants

    /// <summary>
    /// The name of the cache file.
    /// </summary>
    public const string FileName = "catalog.bin";

    /// <summary>
    /// The start of the name of a temporary file.
    /// </summary>
    private const string TemporaryPrefix = "catalog-";

    /// <summary>
    /// The end of the name of a temporary file.
    /// </summary>
    private const string TemporarySuffix = ".tmp";

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The lowercase hexadecimal digits.
    /// </summary>
    private static readonly SearchValues<char> _hexDigits = SearchValues.Create("0123456789abcdef");

    /// <summary>
    /// The age above which a temporary file is deleted.
    /// </summary>
    private static readonly TimeSpan _maxTemporaryAge = TimeSpan.FromHours(24);

    /// <summary>
    /// The cache folder.
    /// </summary>
    private readonly string _folder;

    /// <summary>
    /// The clock.
    /// </summary>
    private readonly TimeProvider _clock;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="GameCatalogCacheStore" /> class.
    /// </summary>
    /// <param name="folder">The cache folder</param>
    /// <param name="clock">The clock</param>
    public GameCatalogCacheStore(string folder, TimeProvider clock)
    {
        _folder = folder;
        _clock = clock;
    }

    #endregion // Constructors

    #region Methods

    /// <summary>
    /// Loads the cache.
    /// </summary>
    /// <param name="expected">The key the cache must belong to</param>
    /// <returns>The content, or the reason there is none</returns>
    public GameCatalogCacheResult Load(GameCatalogKey expected)
    {
        if (Path.IsPathFullyQualified(_folder) == false)
        {
            return GameCatalogCacheResult.FromKind(GameCatalogCacheResultKind.Missing);
        }

        try
        {
            using var stream = new FileStream(Path.Combine(_folder, FileName), FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);

            return GameCatalogCacheFile.Read(stream, stream.Length, expected);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return GameCatalogCacheResult.FromKind(GameCatalogCacheResultKind.Missing);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException or SecurityException)
        {
            return GameCatalogCacheResult.FromKind(GameCatalogCacheResultKind.Invalid);
        }
    }

    /// <summary>
    /// Saves the cache.
    /// </summary>
    /// <param name="key">The key the content belongs to</param>
    /// <param name="catalog">The catalog</param>
    /// <param name="icons">The icons</param>
    /// <returns>The outcome</returns>
    public GameCatalogCacheWriteResult Save(GameCatalogKey key, GameCatalog catalog, GameIconSet icons)
    {
        if (Path.IsPathFullyQualified(_folder) == false)
        {
            return Failed(new ArgumentException("The cache folder is not a full path"));
        }

        try
        {
            Directory.CreateDirectory(_folder);
            Prune();

            var temporary = Path.Combine(_folder, $"{TemporaryPrefix}{Guid.NewGuid():N}{TemporarySuffix}");

            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    if (GameCatalogCacheFile.Write(stream, key, catalog, icons) == false)
                    {
                        throw new InvalidOperationException("The catalog and icons exceed a bound of the cache file");
                    }

                    stream.Flush(true);
                }

                File.Move(temporary, Path.Combine(_folder, FileName), true);
            }
            catch (Exception)
            {
                DeleteQuietly(temporary);

                throw;
            }

            return new GameCatalogCacheWriteResult
                   {
                       IsWritten = true
                   };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or NotSupportedException or SecurityException)
        {
            return Failed(exception);
        }
    }

    /// <summary>
    /// Creates the result of a failed write.
    /// </summary>
    /// <param name="exception">The exception</param>
    /// <returns>The result</returns>
    private static GameCatalogCacheWriteResult Failed(Exception exception)
    {
        return new GameCatalogCacheWriteResult
               {
                   IsWritten = false,
                   Failure = exception
               };
    }

    /// <summary>
    /// Deletes a file and ignores a failure.
    /// </summary>
    /// <param name="path">The path</param>
    private static void DeleteQuietly(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The file stays and is pruned later
        }
    }

    /// <summary>
    /// Checks whether a file name is exactly that of a temporary file: the prefix, 32 lowercase hexadecimal digits and the suffix.
    /// </summary>
    /// <param name="name">The name of the file</param>
    /// <returns><c>true</c> when it is a temporary name</returns>
    private static bool IsTemporaryName(string name)
    {
        if (name.Length != TemporaryPrefix.Length + 32 + TemporarySuffix.Length
            || name.StartsWith(TemporaryPrefix, StringComparison.Ordinal) == false
            || name.EndsWith(TemporarySuffix, StringComparison.Ordinal) == false)
        {
            return false;
        }

        return name.AsSpan(TemporaryPrefix.Length, 32).IndexOfAnyExcept(_hexDigits) < 0;
    }

    /// <summary>
    /// Deletes the top-level files of a temporary name that are more than a day old, a link as a link.
    /// </summary>
    private void Prune()
    {
        var now = _clock.GetUtcNow();

        foreach (var file in new DirectoryInfo(_folder).EnumerateFiles())
        {
            if (IsTemporaryName(file.Name) && now - file.LastWriteTimeUtc > _maxTemporaryAge)
            {
                DeleteQuietly(file.FullName);
            }
        }
    }

    #endregion // Methods
}