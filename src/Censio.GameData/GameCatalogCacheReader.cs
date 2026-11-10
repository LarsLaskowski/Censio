using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Censio.GameData;

/// <summary>
/// Reads the primitives of the catalog cache file forward, feeds the hash and keeps the bounds. After the first failure it
/// returns default values and reads nothing more; no external input reaches an exception.
/// </summary>
internal sealed class GameCatalogCacheReader
{
    #region Constants

    /// <summary>
    /// The length of the hash at the end of the file.
    /// </summary>
    public const int HashLength = 32;

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The strict decoding of strings.
    /// </summary>
    private static readonly UTF8Encoding _utf8 = new(false, true);

    /// <summary>
    /// The stream to read from.
    /// </summary>
    private readonly Stream _source;

    /// <summary>
    /// The hash of everything read.
    /// </summary>
    private readonly IncrementalHash _hash;

    /// <summary>
    /// The length of the content, the file without the hash.
    /// </summary>
    private readonly long _limit;

    /// <summary>
    /// The bytes of the content read so far.
    /// </summary>
    private long _consumed;

    /// <summary>
    /// The bytes read so far that are not PNG data.
    /// </summary>
    private long _otherBytes;

    /// <summary>
    /// The PNG bytes read so far.
    /// </summary>
    private long _pngBytes;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="GameCatalogCacheReader" /> class.
    /// </summary>
    /// <param name="source">The stream to read from</param>
    /// <param name="length">The length of the file in bytes, at least the length of the hash</param>
    /// <param name="hash">The hash of everything read</param>
    public GameCatalogCacheReader(Stream source, long length, IncrementalHash hash)
    {
        _source = source;
        _hash = hash;
        _limit = length - HashLength;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets a value indicating whether a read failed or a bound was exceeded.
    /// </summary>
    public bool IsFailed { get; private set; }

    /// <summary>
    /// Gets the bytes of the content that are left.
    /// </summary>
    public long Remaining => _limit - _consumed;

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Marks the reading as failed.
    /// </summary>
    public void Fail()
    {
        IsFailed = true;
    }

    /// <summary>
    /// Reads one byte.
    /// </summary>
    /// <returns>The byte, or 0 after a failure</returns>
    public byte ReadByte()
    {
        Span<byte> buffer = stackalloc byte[1];

        return Take(buffer, false) ? buffer[0] : (byte)0;
    }

    /// <summary>
    /// Reads a flag stored as 0 or 1.
    /// </summary>
    /// <returns>The flag, or <c>false</c> after a failure</returns>
    public bool ReadFlag()
    {
        var value = ReadByte();

        if (value > 1)
        {
            IsFailed = true;
        }

        return value == 1;
    }

    /// <summary>
    /// Reads a 32-bit integer, little-endian.
    /// </summary>
    /// <returns>The value, or 0 after a failure</returns>
    public int ReadInt32()
    {
        Span<byte> buffer = stackalloc byte[4];

        return Take(buffer, false) ? BinaryPrimitives.ReadInt32LittleEndian(buffer) : 0;
    }

    /// <summary>
    /// Reads a 64-bit integer, little-endian.
    /// </summary>
    /// <returns>The value, or 0 after a failure</returns>
    public long ReadInt64()
    {
        Span<byte> buffer = stackalloc byte[8];

        return Take(buffer, false) ? BinaryPrimitives.ReadInt64LittleEndian(buffer) : 0;
    }

    /// <summary>
    /// Reads raw bytes that are not PNG data.
    /// </summary>
    /// <param name="buffer">The buffer to fill</param>
    /// <returns><c>true</c> when the buffer was filled</returns>
    public bool ReadRaw(Span<byte> buffer)
    {
        return Take(buffer, false);
    }

    /// <summary>
    /// Reads a count whose elements need at least a given number of bytes each.
    /// </summary>
    /// <param name="minElementBytes">The least encoded size of an element</param>
    /// <returns>The count, or 0 after a failure</returns>
    public int ReadCount(int minElementBytes)
    {
        var count = ReadInt32();

        if (IsFailed)
        {
            return 0;
        }

        if (count < 0 || count > Remaining / minElementBytes)
        {
            IsFailed = true;

            return 0;
        }

        return count;
    }

    /// <summary>
    /// Reads a string.
    /// </summary>
    /// <returns>The string, or an empty one after a failure</returns>
    public string ReadString()
    {
        var length = ReadInt32();

        if (IsFailed)
        {
            return string.Empty;
        }

        if (length < 0 || length > GameCatalogCacheFile.MaxStringBytes || length > Remaining)
        {
            IsFailed = true;

            return string.Empty;
        }

        var bytes = new byte[length];

        if (Take(bytes, false) == false)
        {
            return string.Empty;
        }

        try
        {
            return _utf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            IsFailed = true;

            return string.Empty;
        }
    }

    /// <summary>
    /// Reads a string that may be <c>null</c>.
    /// </summary>
    /// <returns>The string, or <c>null</c></returns>
    public string? ReadNullableString()
    {
        return ReadFlag() ? ReadString() : null;
    }

    /// <summary>
    /// Reads the data of one PNG file.
    /// </summary>
    /// <param name="length">The length of the PNG data</param>
    /// <returns>The bytes, or <c>null</c> after a failure</returns>
    public byte[]? ReadPng(int length)
    {
        if (IsFailed)
        {
            return null;
        }

        if (length <= 0 || length > Remaining || _pngBytes + length > GameIconReader.MaxIconSetBytes)
        {
            IsFailed = true;

            return null;
        }

        var bytes = new byte[length];

        if (Take(bytes, true) == false)
        {
            return null;
        }

        _pngBytes += length;

        return bytes;
    }

    /// <summary>
    /// Checks that the content ended, the hash fits and nothing follows it.
    /// </summary>
    /// <returns><c>true</c> when the file is complete and intact</returns>
    public bool Finish()
    {
        if (IsFailed || Remaining != 0)
        {
            return false;
        }

        var stored = new byte[HashLength];

        try
        {
            _source.ReadExactly(stored);

            return CryptographicOperations.FixedTimeEquals(stored, _hash.GetCurrentHash()) && _source.ReadByte() < 0;
        }
        catch (IOException)
        {
            return false;
        }
    }

    /// <summary>
    /// Reads exactly the bytes of a buffer from the content, within the bounds.
    /// </summary>
    /// <param name="buffer">The buffer</param>
    /// <param name="isPng">Whether the bytes are PNG data</param>
    /// <returns><c>true</c> when the buffer was filled</returns>
    private bool Take(Span<byte> buffer, bool isPng)
    {
        if (IsFailed)
        {
            return false;
        }

        if (buffer.Length > Remaining || (isPng == false && _otherBytes + buffer.Length > GameCatalogCacheFile.MaxCatalogBytes))
        {
            IsFailed = true;

            return false;
        }

        try
        {
            _source.ReadExactly(buffer);
        }
        catch (IOException)
        {
            IsFailed = true;

            return false;
        }

        _hash.AppendData(buffer);
        _consumed += buffer.Length;

        if (isPng == false)
        {
            _otherBytes += buffer.Length;
        }

        return true;
    }

    #endregion // Methods
}