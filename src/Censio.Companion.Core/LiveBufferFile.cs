using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;

using Censio.Contracts;

namespace Censio.Companion.Core;

/// <summary>
/// The file format of the live buffer.
/// </summary>
public static class LiveBufferFile
{
    #region Constants

    /// <summary>
    /// The length of the header in bytes.
    /// </summary>
    public const int HeaderLength = 68;

    /// <summary>
    /// The format version.
    /// </summary>
    public const int FormatVersion = 1;

    /// <summary>
    /// The number of digits of the sequence in a file name.
    /// </summary>
    private const int SequenceDigits = 19;

    /// <summary>
    /// The first sequence that is a foreign name, so that the next sequence never overflows.
    /// </summary>
    private const long SequenceLimit = 1000000000000000000L;

    /// <summary>
    /// The extension of a message file.
    /// </summary>
    private const string MessageExtension = ".msg";

    /// <summary>
    /// The extension of a temporary file.
    /// </summary>
    private const string TemporaryExtension = ".tmp";

    /// <summary>
    /// The offset of the format version in the header.
    /// </summary>
    private const int VersionOffset = 4;

    /// <summary>
    /// The offset of the time taken in the header.
    /// </summary>
    private const int TimeOffset = 8;

    /// <summary>
    /// The offset of the identity in the header.
    /// </summary>
    private const int IdentityOffset = 16;

    /// <summary>
    /// The offset of the JSON length in the header.
    /// </summary>
    private const int LengthOffset = 32;

    /// <summary>
    /// The offset of the SHA-256 in the header.
    /// </summary>
    private const int HashOffset = 36;

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The magic of a buffer file.
    /// </summary>
    private static readonly byte[] _magic = "CNSB"u8.ToArray();

    #endregion // Fields

    #region Methods

    /// <summary>
    /// Formats an entry as the bytes of a file.
    /// </summary>
    /// <param name="entry">The entry</param>
    /// <returns>The bytes of the file</returns>
    public static byte[] Format(LiveBufferEntry entry)
    {
        var json = entry.Utf8Json.Span;
        var file = new byte[HeaderLength + json.Length];

        _magic.CopyTo(file, 0);
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(VersionOffset), FormatVersion);
        BinaryPrimitives.WriteInt64LittleEndian(file.AsSpan(TimeOffset), entry.TakenAt.UtcTicks);
        entry.Id.TryWriteBytes(file.AsSpan(IdentityOffset));
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(LengthOffset), json.Length);
        json.CopyTo(file.AsSpan(HeaderLength));

        SHA256.HashData([.. file.AsSpan(0, HashOffset), .. json]).CopyTo(file, HashOffset);

        return file;
    }

    /// <summary>
    /// Reads the header of a file.
    /// </summary>
    /// <param name="header">The first bytes of the file</param>
    /// <param name="fileLength">The length of the file</param>
    /// <returns>The result, with the entry holding an empty JSON</returns>
    public static LiveBufferFileResult ReadHeader(ReadOnlySpan<byte> header, long fileLength)
    {
        if (header.Length < HeaderLength
            || header[..VersionOffset].SequenceEqual(_magic) == false
            || BinaryPrimitives.ReadInt32LittleEndian(header[VersionOffset..]) != FormatVersion)
        {
            return Unreadable();
        }

        var ticks = BinaryPrimitives.ReadInt64LittleEndian(header[TimeOffset..]);
        var id = new Guid(header.Slice(IdentityOffset, 16));
        var length = BinaryPrimitives.ReadInt32LittleEndian(header[LengthOffset..]);

        if (ticks < 0 || ticks > DateTime.MaxValue.Ticks
            || id == Guid.Empty
            || length is < 1 or > ContractReader.MaxMessageBytes
            || fileLength != HeaderLength + (long)length)
        {
            return Unreadable();
        }

        return new LiveBufferFileResult
               {
                   Kind = LiveBufferFileResultKind.Read,
                   Entry = new LiveBufferEntry
                           {
                               Id = id,
                               TakenAt = new DateTimeOffset(ticks, TimeSpan.Zero),
                               Utf8Json = ReadOnlyMemory<byte>.Empty
                           },
                   Length = length
               };
    }

    /// <summary>
    /// Reads a whole file.
    /// </summary>
    /// <param name="file">The bytes of the file</param>
    /// <returns>The result</returns>
    public static LiveBufferFileResult Read(ReadOnlySpan<byte> file)
    {
        var header = ReadHeader(file, file.Length);

        if (header.Kind != LiveBufferFileResultKind.Read || header.Entry is null)
        {
            return Unreadable();
        }

        var json = file[HeaderLength..];
        var expected = file.Slice(HashOffset, SHA256.HashSizeInBytes);
        var actual = SHA256.HashData([.. file[..HashOffset], .. json]);

        if (expected.SequenceEqual(actual) == false)
        {
            return Unreadable();
        }

        return new LiveBufferFileResult
               {
                   Kind = LiveBufferFileResultKind.Read,
                   Entry = new LiveBufferEntry
                           {
                               Id = header.Entry.Id,
                               TakenAt = header.Entry.TakenAt,
                               Utf8Json = json.ToArray()
                           },
                   Length = header.Length
               };
    }

    /// <summary>
    /// Gets the name of a message file.
    /// </summary>
    /// <param name="sequence">The sequence number</param>
    /// <returns>The file name</returns>
    public static string MessageName(long sequence)
    {
        return SequenceName(sequence, MessageExtension);
    }

    /// <summary>
    /// Gets the name of a temporary file.
    /// </summary>
    /// <param name="sequence">The sequence number</param>
    /// <returns>The file name</returns>
    public static string TemporaryName(long sequence)
    {
        return SequenceName(sequence, TemporaryExtension);
    }

    /// <summary>
    /// Tries to parse the name of a message file.
    /// </summary>
    /// <param name="name">The file name</param>
    /// <param name="sequence">The sequence number</param>
    /// <returns><see langword="true" /> when the name is a message name</returns>
    public static bool TryParseMessageName(string name, out long sequence)
    {
        return TryParseName(name, MessageExtension, out sequence);
    }

    /// <summary>
    /// Tries to parse the name of a temporary file.
    /// </summary>
    /// <param name="name">The file name</param>
    /// <param name="sequence">The sequence number</param>
    /// <returns><see langword="true" /> when the name is a temporary name</returns>
    public static bool TryParseTemporaryName(string name, out long sequence)
    {
        return TryParseName(name, TemporaryExtension, out sequence);
    }

    /// <summary>
    /// Creates the result of an unreadable file or header.
    /// </summary>
    /// <returns>The result</returns>
    private static LiveBufferFileResult Unreadable()
    {
        return new LiveBufferFileResult
               {
                   Kind = LiveBufferFileResultKind.Unreadable
               };
    }

    /// <summary>
    /// Gets a file name from a sequence and an extension.
    /// </summary>
    /// <param name="sequence">The sequence number</param>
    /// <param name="extension">The extension with its dot</param>
    /// <returns>The file name</returns>
    private static string SequenceName(long sequence, string extension)
    {
        return sequence.ToString(new string('0', SequenceDigits), CultureInfo.InvariantCulture) + extension;
    }

    /// <summary>
    /// Parses a file name of exactly 19 ASCII digits below 10^18 followed by an extension.
    /// </summary>
    /// <param name="name">The file name</param>
    /// <param name="extension">The extension with its dot, in lower case</param>
    /// <param name="sequence">The sequence number</param>
    /// <returns><see langword="true" /> when the name has that form</returns>
    private static bool TryParseName(string name, string extension, out long sequence)
    {
        sequence = 0;

        if (name.Length != SequenceDigits + extension.Length
            || name.EndsWith(extension, StringComparison.Ordinal) == false)
        {
            return false;
        }

        var digits = name.AsSpan(0, SequenceDigits);

        return digits.IndexOfAnyExceptInRange('0', '9') < 0
               && long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out sequence)
               && sequence < SequenceLimit;
    }

    #endregion // Methods
}