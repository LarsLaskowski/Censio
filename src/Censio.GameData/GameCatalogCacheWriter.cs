using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Censio.GameData;

/// <summary>
/// Writes the primitives of the catalog cache file, feeds the hash and keeps the bounds. After the first bound is exceeded it
/// writes nothing more.
/// </summary>
internal sealed class GameCatalogCacheWriter
{
    #region Fields

    /// <summary>
    /// The strict encoding of strings.
    /// </summary>
    private static readonly UTF8Encoding _utf8 = new(false, true);

    /// <summary>
    /// The stream to write to.
    /// </summary>
    private readonly Stream _destination;

    /// <summary>
    /// The hash of everything written.
    /// </summary>
    private readonly IncrementalHash _hash;

    /// <summary>
    /// The bytes written that are not PNG data.
    /// </summary>
    private long _otherBytes;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="GameCatalogCacheWriter" /> class.
    /// </summary>
    /// <param name="destination">The stream to write to</param>
    /// <param name="hash">The hash of everything written</param>
    public GameCatalogCacheWriter(Stream destination, IncrementalHash hash)
    {
        _destination = destination;
        _hash = hash;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets a value indicating whether a bound was exceeded.
    /// </summary>
    public bool IsFailed { get; private set; }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Writes one byte.
    /// </summary>
    /// <param name="value">The value</param>
    public void WriteByte(byte value)
    {
        WriteOther([value]);
    }

    /// <summary>
    /// Writes a flag as one byte.
    /// </summary>
    /// <param name="value">The value</param>
    public void WriteFlag(bool value)
    {
        WriteByte(value ? (byte)1 : (byte)0);
    }

    /// <summary>
    /// Writes a 32-bit integer, little-endian.
    /// </summary>
    /// <param name="value">The value</param>
    public void WriteInt32(int value)
    {
        Span<byte> buffer = stackalloc byte[4];

        BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
        WriteOther(buffer);
    }

    /// <summary>
    /// Writes a 64-bit integer, little-endian.
    /// </summary>
    /// <param name="value">The value</param>
    public void WriteInt64(long value)
    {
        Span<byte> buffer = stackalloc byte[8];

        BinaryPrimitives.WriteInt64LittleEndian(buffer, value);
        WriteOther(buffer);
    }

    /// <summary>
    /// Writes raw bytes that are not PNG data.
    /// </summary>
    /// <param name="bytes">The bytes</param>
    public void WriteRaw(ReadOnlySpan<byte> bytes)
    {
        WriteOther(bytes);
    }

    /// <summary>
    /// Writes a string as its byte length and its UTF-8 bytes.
    /// </summary>
    /// <param name="value">The string</param>
    public void WriteString(string value)
    {
        if (IsFailed)
        {
            return;
        }

        byte[] bytes;

        try
        {
            bytes = _utf8.GetBytes(value);
        }
        catch (EncoderFallbackException)
        {
            IsFailed = true;

            return;
        }

        if (bytes.Length > GameCatalogCacheFile.MaxStringBytes)
        {
            IsFailed = true;

            return;
        }

        WriteInt32(bytes.Length);
        WriteOther(bytes);
    }

    /// <summary>
    /// Writes a string that may be <c>null</c> as a flag and, if present, the string.
    /// </summary>
    /// <param name="value">The string, or <c>null</c></param>
    public void WriteNullableString(string? value)
    {
        WriteFlag(value is not null);

        if (value is not null)
        {
            WriteString(value);
        }
    }

    /// <summary>
    /// Writes PNG data.
    /// </summary>
    /// <param name="png">The bytes of the PNG file</param>
    public void WritePng(ReadOnlyMemory<byte> png)
    {
        if (IsFailed)
        {
            return;
        }

        _destination.Write(png.Span);
        _hash.AppendData(png.Span);
    }

    /// <summary>
    /// Marks the writing as failed because a bound would be exceeded.
    /// </summary>
    public void Fail()
    {
        IsFailed = true;
    }

    /// <summary>
    /// Writes the hash of everything written before.
    /// </summary>
    public void WriteHash()
    {
        if (IsFailed)
        {
            return;
        }

        _destination.Write(_hash.GetCurrentHash());
    }

    /// <summary>
    /// Writes bytes that are not PNG data, unless that would exceed the catalog bound.
    /// </summary>
    /// <param name="bytes">The bytes</param>
    private void WriteOther(ReadOnlySpan<byte> bytes)
    {
        if (IsFailed)
        {
            return;
        }

        if (_otherBytes + bytes.Length > GameCatalogCacheFile.MaxCatalogBytes)
        {
            IsFailed = true;

            return;
        }

        _otherBytes += bytes.Length;
        _destination.Write(bytes);
        _hash.AppendData(bytes);
    }

    #endregion // Methods
}