using System.Buffers.Binary;
using System.Security.Cryptography;

using Censio.Contracts;

namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for <see cref="LiveBufferFile" />.
/// </summary>
[TestClass]
public sealed class LiveBufferFileTests
{
    #region Constants

    /// <summary>
    /// The UTC ticks of 2026-11-23 12:00, the time the sample message was taken.
    /// </summary>
    private const long SampleTicks = 639310320000000000L;

    /// <summary>
    /// The ticks of <see cref="DateTime.MaxValue" />, the latest time a file may hold.
    /// </summary>
    private const long MaxTicks = 3155378975999999999L;

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
    /// The offset of the SHA-256 in the header; the bytes before it are hashed with the JSON.
    /// </summary>
    private const int HashOffset = 36;

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The identity of the sample message.
    /// </summary>
    private static readonly Guid _sampleId = new("6f1c2a9e-4b3d-4e8f-9a01-23456789abcd");

    /// <summary>
    /// The magic of a buffer file.
    /// </summary>
    private static readonly byte[] _magic = "CNSB"u8.ToArray();

    /// <summary>
    /// The JSON of the sample message.
    /// </summary>
    private static readonly byte[] _sampleJson = """{"type":"tick","id":"6f1c2a9e-4b3d-4e8f-9a01-23456789abcd","islands":[]}"""u8.ToArray();

    #endregion // Fields

    #region Methods

    /// <summary>
    /// A formatted entry is read back with its identity, its time taken, its JSON and the JSON's length, and the file is
    /// the header of 68 bytes followed by the JSON (AC8).
    /// </summary>
    [TestMethod]
    [TestCategory("AC8")]
    public void LiveBufferFileFormatReadsBack()
    {
        // Arrange
        var entry = SampleEntry();
        var file = LiveBufferFile.Format(entry);

        // Act
        var result = LiveBufferFile.Read(file);

        // Assert
        Assert.HasCount(LiveBufferFile.HeaderLength + _sampleJson.Length, file, "The file should be the header of 68 bytes followed by the JSON");
        Assert.AreEqual(LiveBufferFileResultKind.Read, result.Kind, "A formatted entry should be read back");
        Assert.IsNotNull(result.Entry, "A file that is read should carry its entry");
        Assert.AreEqual(_sampleId, result.Entry.Id, "The identity should be read back");
        Assert.AreEqual(entry.TakenAt, result.Entry.TakenAt, "The time taken should be read back as the same instant");
        Assert.AreSequenceEqual(_sampleJson, result.Entry.Utf8Json.ToArray(), "The JSON should be read back unchanged");
        Assert.AreEqual(_sampleJson.Length, result.Length, "The length should be the JSON's length");
    }

    /// <summary>
    /// The file is the little-endian header <c>CNSB</c>, version 1, UTC ticks, identity, JSON length and the SHA-256 of
    /// header bytes 0 to 35 followed by the JSON, then the JSON (AC8).
    /// </summary>
    [TestMethod]
    [TestCategory("AC8")]
    public void LiveBufferFileFormatWritesHeaderLayout()
    {
        // Arrange
        var entry = SampleEntry();
        var expected = BuildFile(_magic, LiveBufferFile.FormatVersion, SampleTicks, _sampleId.ToByteArray(), _sampleJson.Length, _sampleJson);

        // Act
        var file = LiveBufferFile.Format(entry);

        // Assert
        Assert.AreEqual(1, LiveBufferFile.FormatVersion, "The format version should be 1");
        Assert.AreEqual(68, LiveBufferFile.HeaderLength, "The header should be 68 bytes long");
        Assert.AreEqual(SampleTicks, entry.TakenAt.UtcTicks, "The sample time should be the documented UTC ticks");
        Assert.AreSequenceEqual(expected, file, "The file should hold magic, version, UTC ticks, identity, length, the SHA-256 of bytes 0 to 35 and the JSON, then the JSON");
    }

    /// <summary>
    /// A file of a damaged or foreign form is unreadable without an exception: a cut header, another magic or version,
    /// an empty identity, a time out of range, a length of 0, below 0 or above the contract's message bound, a file
    /// length other than 68 plus the length, and a changed JSON, identity, time or hash byte the hash then no longer
    /// matches (AC8).
    /// </summary>
    /// <param name="form">The damaged form</param>
    [TestMethod]
    [TestCategory("AC8")]
    [DataRow("empty", DisplayName = "Empty file")]
    [DataRow("short header", DisplayName = "Header of 67 bytes")]
    [DataRow("magic", DisplayName = "Other magic")]
    [DataRow("magic lower case", DisplayName = "Magic in lower case")]
    [DataRow("version 0", DisplayName = "Version 0")]
    [DataRow("version 2", DisplayName = "Version 2")]
    [DataRow("zero id", DisplayName = "All-zero identity")]
    [DataRow("time negative", DisplayName = "Time below 0")]
    [DataRow("time above max", DisplayName = "Time above DateTime.MaxValue")]
    [DataRow("length 0", DisplayName = "Length 0")]
    [DataRow("length negative", DisplayName = "Length below 0")]
    [DataRow("length above max", DisplayName = "Length above 25,165,824")]
    [DataRow("file longer", DisplayName = "File one byte longer than the header says")]
    [DataRow("file shorter", DisplayName = "File one byte shorter than the header says")]
    [DataRow("length field larger", DisplayName = "Length one above the JSON, hash valid")]
    [DataRow("json byte", DisplayName = "A JSON byte changed")]
    [DataRow("identity byte", DisplayName = "An identity byte changed, JSON untouched")]
    [DataRow("time byte", DisplayName = "A time byte changed, JSON untouched")]
    [DataRow("hash byte", DisplayName = "A hash byte changed")]
    public void LiveBufferFileReadRefusesDamagedForms(string form)
    {
        // Arrange
        var file = DamagedFile(form);

        // Act
        var result = LiveBufferFile.Read(file);

        // Assert
        Assert.AreEqual(LiveBufferFileResultKind.Unreadable, result.Kind, $"A file of the form {form} should be unreadable");
        Assert.IsNull(result.Entry, $"An unreadable file of the form {form} should carry no entry");
    }

    /// <summary>
    /// A header of a damaged or foreign form, or a file length other than 68 plus the header's length, is unreadable
    /// without an exception (AC8).
    /// </summary>
    /// <param name="form">The damaged form</param>
    [TestMethod]
    [TestCategory("AC8")]
    [DataRow("empty", DisplayName = "Empty file")]
    [DataRow("short header", DisplayName = "Header of 67 bytes")]
    [DataRow("magic", DisplayName = "Other magic")]
    [DataRow("magic lower case", DisplayName = "Magic in lower case")]
    [DataRow("version 0", DisplayName = "Version 0")]
    [DataRow("version 2", DisplayName = "Version 2")]
    [DataRow("zero id", DisplayName = "All-zero identity")]
    [DataRow("time negative", DisplayName = "Time below 0")]
    [DataRow("time above max", DisplayName = "Time above DateTime.MaxValue")]
    [DataRow("length 0", DisplayName = "Length 0")]
    [DataRow("length negative", DisplayName = "Length below 0")]
    [DataRow("length above max", DisplayName = "Length above 25,165,824")]
    [DataRow("file longer", DisplayName = "File one byte longer than the header says")]
    [DataRow("file shorter", DisplayName = "File one byte shorter than the header says")]
    [DataRow("length field larger", DisplayName = "Length one above the JSON")]
    public void LiveBufferFileReadHeaderRefusesDamagedForms(string form)
    {
        // Arrange
        var (header, fileLength) = DamagedHeader(form);

        // Act
        var result = LiveBufferFile.ReadHeader(header, fileLength);

        // Assert
        Assert.AreEqual(LiveBufferFileResultKind.Unreadable, result.Kind, $"A header of the form {form} should be unreadable");
        Assert.IsNull(result.Entry, $"An unreadable header of the form {form} should carry no entry");
    }

    /// <summary>
    /// A header with a time from 0 to <see cref="DateTime.MaxValue" /> and a length from 1 to the contract's message
    /// bound, in a file of 68 bytes plus that length, is read with its identity, time and length and an empty JSON (AC8).
    /// </summary>
    /// <param name="ticks">The UTC ticks of the time taken</param>
    /// <param name="length">The JSON length</param>
    [TestMethod]
    [TestCategory("AC8")]
    [DataRow(SampleTicks, 1, DisplayName = "Length 1")]
    [DataRow(0L, ContractReader.MaxMessageBytes, DisplayName = "Time 0, length 25,165,824")]
    [DataRow(MaxTicks, 72, DisplayName = "Time DateTime.MaxValue")]
    public void LiveBufferFileReadHeaderReadsIdentityTimeAndLength(long ticks, int length)
    {
        // Arrange
        var header = BuildFile(_magic, LiveBufferFile.FormatVersion, ticks, _sampleId.ToByteArray(), length, []);

        // Act
        var result = LiveBufferFile.ReadHeader(header, LiveBufferFile.HeaderLength + (long)length);

        // Assert
        Assert.AreEqual(LiveBufferFileResultKind.Read, result.Kind, "A valid header should be read");
        Assert.IsNotNull(result.Entry, "A header that is read should carry its entry");
        Assert.AreEqual(_sampleId, result.Entry.Id, "The identity should be read from the header");
        Assert.AreEqual(new DateTimeOffset(ticks, TimeSpan.Zero), result.Entry.TakenAt, "The time taken should be read from the header as UTC");
        Assert.IsEmpty(result.Entry.Utf8Json.ToArray(), "A header should be read with an empty JSON");
        Assert.AreEqual(length, result.Length, "The length should be read from the header");
    }

    /// <summary>
    /// A message name is the sequence in 19 decimal digits followed by <c>.msg</c> (AC8).
    /// </summary>
    /// <param name="sequence">The sequence</param>
    /// <param name="expected">The expected name</param>
    [TestMethod]
    [TestCategory("AC8")]
    [DataRow(0L, "0000000000000000000.msg", DisplayName = "Sequence 0")]
    [DataRow(5L, "0000000000000000005.msg", DisplayName = "Sequence 5")]
    [DataRow(999999999999999999L, "0999999999999999999.msg", DisplayName = "Largest sequence")]
    public void LiveBufferFileMessageNameIsNineteenDigitsAndExtension(long sequence, string expected)
    {
        // Act
        var name = LiveBufferFile.MessageName(sequence);

        // Assert
        Assert.AreEqual(expected, name, "The message name should be the sequence in 19 digits followed by .msg");
    }

    /// <summary>
    /// A temporary name is the sequence in 19 decimal digits followed by <c>.tmp</c> (AC8).
    /// </summary>
    /// <param name="sequence">The sequence</param>
    /// <param name="expected">The expected name</param>
    [TestMethod]
    [TestCategory("AC8")]
    [DataRow(0L, "0000000000000000000.tmp", DisplayName = "Sequence 0")]
    [DataRow(5L, "0000000000000000005.tmp", DisplayName = "Sequence 5")]
    [DataRow(999999999999999999L, "0999999999999999999.tmp", DisplayName = "Largest sequence")]
    public void LiveBufferFileTemporaryNameIsNineteenDigitsAndExtension(long sequence, string expected)
    {
        // Act
        var name = LiveBufferFile.TemporaryName(sequence);

        // Assert
        Assert.AreEqual(expected, name, "The temporary name should be the sequence in 19 digits followed by .tmp");
    }

    /// <summary>
    /// A message name of 19 digits below 10^18 followed by <c>.msg</c> is parsed into its sequence (AC8).
    /// </summary>
    /// <param name="name">The file name</param>
    /// <param name="expected">The expected sequence</param>
    [TestMethod]
    [TestCategory("AC8")]
    [DataRow("0000000000000000000.msg", 0L, DisplayName = "Sequence 0")]
    [DataRow("0000000000000000005.msg", 5L, DisplayName = "Sequence 5")]
    [DataRow("0999999999999999999.msg", 999999999999999999L, DisplayName = "Largest sequence")]
    public void LiveBufferFileTryParseMessageNameReadsSequence(string name, long expected)
    {
        // Act
        var parsed = LiveBufferFile.TryParseMessageName(name, out var sequence);

        // Assert
        Assert.IsTrue(parsed, $"The name {name} should be a message name");
        Assert.AreEqual(expected, sequence, "The sequence should be the number of the name");
    }

    /// <summary>
    /// A message name with a sequence from 10^18 is a foreign name, so the next sequence never overflows (AC8).
    /// </summary>
    /// <param name="digits">The 19 digits of the name</param>
    [TestMethod]
    [TestCategory("AC8")]
    [DataRow("1000000000000000000", DisplayName = "10^18")]
    [DataRow("9223372036854775806", DisplayName = "Largest 64-bit integer minus one")]
    [DataRow("9223372036854775807", DisplayName = "Largest 64-bit integer")]
    [DataRow("9223372036854775808", DisplayName = "Beyond a 64-bit integer")]
    [DataRow("9999999999999999999", DisplayName = "Nineteen nines")]
    public void LiveBufferFileTryParseMessageNameRefusesLargeSequence(string digits)
    {
        // Act
        var parsed = LiveBufferFile.TryParseMessageName($"{digits}.msg", out _);

        // Assert
        Assert.HasCount(19, digits, "The name of the case should have the 19 digits of a message name");
        Assert.IsFalse(parsed, $"The name {digits}.msg should be a foreign name");
    }

    /// <summary>
    /// A name that is not exactly 19 ASCII digits followed by <c>.msg</c> in lower case is a foreign name (AC8).
    /// </summary>
    /// <param name="name">The file name</param>
    [TestMethod]
    [TestCategory("AC8")]
    [DataRow("", DisplayName = "Empty")]
    [DataRow("5.msg", DisplayName = "One digit")]
    [DataRow("000000000000000005.msg", DisplayName = "18 digits")]
    [DataRow("00000000000000000005.msg", DisplayName = "20 digits")]
    [DataRow("0000000000000000005.MSG", DisplayName = "Extension in upper case")]
    [DataRow("0000000000000000005.Msg", DisplayName = "Extension in mixed case")]
    [DataRow("0000000000000000005.tmp", DisplayName = "Temporary name")]
    [DataRow("0000000000000000005.msg.tmp", DisplayName = "Two extensions")]
    [DataRow("0000000000000000005.msgx", DisplayName = "Longer extension")]
    [DataRow("0000000000000000005msg", DisplayName = "No dot")]
    [DataRow("0000000000000000005", DisplayName = "No extension")]
    [DataRow(".msg", DisplayName = "Extension only")]
    [DataRow(" 0000000000000000005.msg", DisplayName = "Leading space")]
    [DataRow("0000000000000000005.msg ", DisplayName = "Trailing space")]
    [DataRow("-000000000000000005.msg", DisplayName = "Minus sign")]
    [DataRow("+000000000000000005.msg", DisplayName = "Plus sign")]
    [DataRow("000000000000000000a.msg", DisplayName = "A letter")]
    [DataRow("00000000000000000０5.msg", DisplayName = "A full-width digit")]
    [DataRow("sub/0000000000000000005.msg", DisplayName = "In a subfolder")]
    public void LiveBufferFileTryParseMessageNameRefusesForeignNames(string name)
    {
        // Act
        var parsed = LiveBufferFile.TryParseMessageName(name, out _);

        // Assert
        Assert.IsFalse(parsed, $"The name {name} should be a foreign name");
    }

    /// <summary>
    /// A temporary name of 19 digits below 10^18 followed by <c>.tmp</c> is parsed into its sequence (AC8).
    /// </summary>
    /// <param name="name">The file name</param>
    /// <param name="expected">The expected sequence</param>
    [TestMethod]
    [TestCategory("AC8")]
    [DataRow("0000000000000000000.tmp", 0L, DisplayName = "Sequence 0")]
    [DataRow("0000000000000000005.tmp", 5L, DisplayName = "Sequence 5")]
    [DataRow("0999999999999999999.tmp", 999999999999999999L, DisplayName = "Largest sequence")]
    public void LiveBufferFileTryParseTemporaryNameReadsSequence(string name, long expected)
    {
        // Act
        var parsed = LiveBufferFile.TryParseTemporaryName(name, out var sequence);

        // Assert
        Assert.IsTrue(parsed, $"The name {name} should be a temporary name");
        Assert.AreEqual(expected, sequence, "The sequence should be the number of the name");
    }

    /// <summary>
    /// A name that is not exactly 19 ASCII digits below 10^18 followed by <c>.tmp</c> in lower case is a foreign name
    /// (AC8).
    /// </summary>
    /// <param name="name">The file name</param>
    [TestMethod]
    [TestCategory("AC8")]
    [DataRow("", DisplayName = "Empty")]
    [DataRow("1000000000000000000.tmp", DisplayName = "10^18")]
    [DataRow("9223372036854775807.tmp", DisplayName = "Largest 64-bit integer")]
    [DataRow("9999999999999999999.tmp", DisplayName = "Nineteen nines")]
    [DataRow("000000000000000005.tmp", DisplayName = "18 digits")]
    [DataRow("00000000000000000005.tmp", DisplayName = "20 digits")]
    [DataRow("0000000000000000005.TMP", DisplayName = "Extension in upper case")]
    [DataRow("0000000000000000005.msg", DisplayName = "Message name")]
    [DataRow("0000000000000000005.tmp.msg", DisplayName = "Two extensions")]
    [DataRow(" 0000000000000000005.tmp", DisplayName = "Leading space")]
    [DataRow("-000000000000000005.tmp", DisplayName = "Minus sign")]
    [DataRow("000000000000000000a.tmp", DisplayName = "A letter")]
    [DataRow("sub/0000000000000000005.tmp", DisplayName = "In a subfolder")]
    public void LiveBufferFileTryParseTemporaryNameRefusesForeignNames(string name)
    {
        // Act
        var parsed = LiveBufferFile.TryParseTemporaryName(name, out _);

        // Assert
        Assert.IsFalse(parsed, $"The name {name} should be a foreign name");
    }

    /// <summary>
    /// Creates the sample entry, taken at 2026-11-23 13:00 at an offset of one hour, which is 12:00 UTC.
    /// </summary>
    /// <returns>The entry</returns>
    private static LiveBufferEntry SampleEntry()
    {
        return new LiveBufferEntry
               {
                   Id = _sampleId,
                   TakenAt = new DateTimeOffset(2026, 11, 23, 13, 0, 0, TimeSpan.FromHours(1)),
                   Utf8Json = _sampleJson
               };
    }

    /// <summary>
    /// Builds the bytes of a buffer file as the plan lays it out, with the SHA-256 of header bytes 0 to 35 followed by
    /// the JSON.
    /// </summary>
    /// <param name="magic">The four magic bytes</param>
    /// <param name="version">The format version</param>
    /// <param name="ticks">The UTC ticks of the time taken</param>
    /// <param name="identity">The 16 identity bytes</param>
    /// <param name="length">The JSON length written in the header</param>
    /// <param name="json">The JSON written after the header</param>
    /// <returns>The bytes of the file</returns>
    private static byte[] BuildFile(byte[] magic, int version, long ticks, byte[] identity, int length, byte[] json)
    {
        var file = new byte[LiveBufferFile.HeaderLength + json.Length];

        magic.CopyTo(file, 0);
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(VersionOffset), version);
        BinaryPrimitives.WriteInt64LittleEndian(file.AsSpan(TimeOffset), ticks);
        identity.CopyTo(file, IdentityOffset);
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(LengthOffset), length);
        json.CopyTo(file, LiveBufferFile.HeaderLength);

        var hash = SHA256.HashData([.. file.AsSpan(0, HashOffset), .. json]);

        hash.CopyTo(file, HashOffset);

        return file;
    }

    /// <summary>
    /// Builds a valid file of the sample message.
    /// </summary>
    /// <returns>The bytes of the file</returns>
    private static byte[] ValidFile()
    {
        return BuildFile(_magic, LiveBufferFile.FormatVersion, SampleTicks, _sampleId.ToByteArray(), _sampleJson.Length, _sampleJson);
    }

    /// <summary>
    /// Builds a file of the sample message that differs in one header field, with its hash computed over the changed
    /// header, so that only that field can make it unreadable.
    /// </summary>
    /// <param name="magic">The four magic bytes</param>
    /// <param name="version">The format version</param>
    /// <param name="ticks">The UTC ticks of the time taken</param>
    /// <param name="identity">The 16 identity bytes</param>
    /// <returns>The bytes of the file</returns>
    private static byte[] ChangedField(byte[] magic, int version, long ticks, byte[] identity)
    {
        return BuildFile(magic, version, ticks, identity, _sampleJson.Length, _sampleJson);
    }

    /// <summary>
    /// Copies a file with one byte flipped and its hash left as it was.
    /// </summary>
    /// <param name="file">The file</param>
    /// <param name="offset">The offset of the byte</param>
    /// <returns>The changed copy</returns>
    private static byte[] FlipByte(byte[] file, int offset)
    {
        var copy = file.ToArray();

        copy[offset] ^= 0x01;

        return copy;
    }

    /// <summary>
    /// Builds a file of a damaged or foreign form.
    /// </summary>
    /// <param name="form">The form, as the DataRows of <see cref="LiveBufferFileReadRefusesDamagedForms" /> name it</param>
    /// <returns>The bytes of the file</returns>
    private static byte[] DamagedFile(string form)
    {
        var identity = _sampleId.ToByteArray();
        var version = LiveBufferFile.FormatVersion;

        return form switch
               {
                   "empty" => [],
                   "short header" => ValidFile()[..(LiveBufferFile.HeaderLength - 1)],
                   "magic" => ChangedField("CNSA"u8.ToArray(), version, SampleTicks, identity),
                   "magic lower case" => ChangedField("cnsb"u8.ToArray(), version, SampleTicks, identity),
                   "version 0" => ChangedField(_magic, 0, SampleTicks, identity),
                   "version 2" => ChangedField(_magic, 2, SampleTicks, identity),
                   "zero id" => ChangedField(_magic, version, SampleTicks, new byte[16]),
                   "time negative" => ChangedField(_magic, version, -1L, identity),
                   "time above max" => ChangedField(_magic, version, MaxTicks + 1, identity),
                   "length 0" => BuildFile(_magic, version, SampleTicks, identity, 0, []),
                   "length negative" => BuildFile(_magic, version, SampleTicks, identity, -1, _sampleJson),
                   "length above max" => BuildFile(_magic, version, SampleTicks, identity, ContractReader.MaxMessageBytes + 1, new byte[ContractReader.MaxMessageBytes + 1]),
                   "file longer" => [.. ValidFile(), (byte)' '],
                   "file shorter" => ValidFile()[..^1],
                   "length field larger" => BuildFile(_magic, version, SampleTicks, identity, _sampleJson.Length + 1, _sampleJson),
                   "json byte" => FlipByte(ValidFile(), LiveBufferFile.HeaderLength + 2),
                   "identity byte" => FlipByte(ValidFile(), IdentityOffset + 4),
                   "time byte" => FlipByte(ValidFile(), TimeOffset),
                   _ => FlipByte(ValidFile(), HashOffset + 4)
               };
    }

    /// <summary>
    /// Builds the header and the file length of a damaged or foreign form.
    /// </summary>
    /// <param name="form">The form, as the DataRows of <see cref="LiveBufferFileReadHeaderRefusesDamagedForms" /> name it</param>
    /// <returns>The first bytes of the file, at most the header, and the file length</returns>
    private static (byte[] Header, long FileLength) DamagedHeader(string form)
    {
        var identity = _sampleId.ToByteArray();

        return form switch
               {
                   "length above max" => (BuildFile(_magic, LiveBufferFile.FormatVersion, SampleTicks, identity, ContractReader.MaxMessageBytes + 1, []), LiveBufferFile.HeaderLength + ContractReader.MaxMessageBytes + 1L),
                   _ => HeaderOf(DamagedFile(form))
               };
    }

    /// <summary>
    /// Gets the first bytes of a file, at most the header, and its length.
    /// </summary>
    /// <param name="file">The file</param>
    /// <returns>The header and the file length</returns>
    private static (byte[] Header, long FileLength) HeaderOf(byte[] file)
    {
        return (file[..Math.Min(file.Length, LiveBufferFile.HeaderLength)], file.LongLength);
    }

    #endregion // Methods
}