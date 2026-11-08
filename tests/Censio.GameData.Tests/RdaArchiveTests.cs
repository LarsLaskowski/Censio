using System.Text;

namespace Censio.GameData.Tests;

/// <summary>
/// Tests for <see cref="RdaArchive" />.
/// </summary>
[TestClass]
public sealed class RdaArchiveTests
{
    #region Constants

    /// <summary>
    /// A value above <see cref="long.MaxValue" />.
    /// </summary>
    private const ulong AboveInt64Max = 0x8000_0000_0000_0000UL;

    #endregion // Constants

    #region Methods

    #region AC3

    /// <summary>
    /// Opening lists every entry of every block with its path, size and block flags.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void RdaArchiveOpenListsEntriesWithPathsSizesAndFlags()
    {
        // Arrange
        var built = new RdaArchiveBuilder().AddBlock(0, ("data/a.xml", Text("alpha")), ("data/b.txt", Text("beta beta")))
                                           .AddBlock(RdaArchiveBuilder.Compressed, ("data/c.dds", Text("gamma ", 10)))
                                           .Build();

        // Act
        using var archive = OpenArchive(built.Bytes);

        // Assert
        Assert.AreSequenceEqual(["data/a.xml", "data/b.txt", "data/c.dds"], archive.Entries.Select(entry => entry.Path).ToArray(), "The entries should be listed with their paths in archive order");
        Assert.AreSequenceEqual([5, 9, 60], archive.Entries.Select(entry => entry.Size).ToArray(), "The entries should be listed with their decompressed sizes");
        Assert.AreSequenceEqual([RdaBlockAttributes.None, RdaBlockAttributes.None, RdaBlockAttributes.Compressed], archive.Entries.Select(entry => entry.Flags).ToArray(), "The entries should carry the flags of their block");
        Assert.AreEqual(0, archive.EncryptedBlockCount, "No block is encrypted");
    }

    /// <summary>
    /// A stored entry is returned as it lies in the archive.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void RdaArchiveReadStoredEntryReturnsContent()
    {
        // Arrange
        var built = new RdaArchiveBuilder().AddBlock(0, ("data/a.xml", Text("alpha")), ("data/b.txt", Text("beta beta"))).Build();
        using var archive = OpenArchive(built.Bytes);

        // Act
        var result = archive.Read("data/b.txt");

        // Assert
        AssertContent(Text("beta beta"), result);
    }

    /// <summary>
    /// A compressed entry is returned decompressed.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void RdaArchiveReadCompressedEntryReturnsDecompressedContent()
    {
        // Arrange
        var built = new RdaArchiveBuilder().AddBlock(RdaArchiveBuilder.Compressed, ("data/a.xml", Text("alpha ", 40)), ("data/b.txt", Text("beta ", 30))).Build();
        using var archive = OpenArchive(built.Bytes);

        // Act
        var result = archive.Read("data/b.txt");

        // Assert
        AssertContent(Text("beta ", 30), result);
    }

    /// <summary>
    /// An entry of a memory-resident block, plain or compressed, is cut out of the block's data.
    /// </summary>
    /// <param name="flags">The flags of the block</param>
    [TestMethod]
    [TestCategory("AC3")]
    [DataRow(4u)]
    [DataRow(5u)]
    public void RdaArchiveReadEntryOfMemoryResidentBlockReturnsContent(uint flags)
    {
        // Arrange
        var built = new RdaArchiveBuilder().AddBlock(flags, ("data/a.xml", Text("alpha ", 8)), ("data/b.txt", Text("beta ", 12)), ("data/c.txt", Text("gamma ", 5))).Build();
        using var archive = OpenArchive(built.Bytes);

        // Act
        var result = archive.Read("data/b.txt");

        // Assert
        AssertContent(Text("beta ", 12), result);
    }

    /// <summary>
    /// Entries of several blocks of different kinds are each returned.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void RdaArchiveReadEntriesAcrossSeveralBlocksReturnsEach()
    {
        // Arrange
        var built = new RdaArchiveBuilder().AddBlock(0, ("data/stored.txt", Text("stored")))
                                           .AddBlock(RdaArchiveBuilder.Compressed, ("data/packed.txt", Text("packed ", 20)))
                                           .AddBlock(RdaArchiveBuilder.MemoryResident | RdaArchiveBuilder.Compressed, ("data/one.txt", Text("one ", 9)), ("data/two.txt", Text("two ", 7)))
                                           .Build();
        using var archive = OpenArchive(built.Bytes);

        // Act
        string[] paths = ["data/stored.txt", "data/packed.txt", "data/one.txt", "data/two.txt"];
        var results = paths.Select(archive.Read).ToArray();

        // Assert
        AssertContent(Text("stored"), results[0]);
        AssertContent(Text("packed ", 20), results[1]);
        AssertContent(Text("one ", 9), results[2]);
        AssertContent(Text("two ", 7), results[3]);
    }

    /// <summary>
    /// A block header, a directory and an entry above 4 GiB are read without truncating an offset to 32 bits.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void RdaArchiveReadEntryBeyondFourGibibytesReturnsContent()
    {
        // Arrange
        const long dataOffset = 4_294_967_296L + 1000;
        var content = Text("invented far content");
        var directoryOffset = dataOffset + content.Length;
        var entry = RdaArchiveBuilder.DirectoryEntry("data/far.txt", (ulong)dataOffset, (ulong)content.Length, (ulong)content.Length);
        var headerOffset = directoryOffset + entry.Length;
        var length = headerOffset + RdaArchiveBuilder.BlockHeaderSize;
        var stream = new SparseStream(length).WithRegion(0, RdaArchiveBuilder.Header((ulong)headerOffset))
                                             .WithRegion(dataOffset, content)
                                             .WithRegion(directoryOffset, entry)
                                             .WithRegion(headerOffset, RdaArchiveBuilder.BlockHeader(0, 1, RdaArchiveBuilder.EntrySize, RdaArchiveBuilder.EntrySize, (ulong)length));
        using var archive = OpenArchive(stream);

        // Act
        var result = archive.Read("data/far.txt");

        // Assert
        AssertContent(content, result);
    }

    /// <summary>
    /// A deleted block adds no entries.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void RdaArchiveOpenSkipsDeletedBlock()
    {
        // Arrange
        var built = new RdaArchiveBuilder().AddBlock(0, ("data/a.txt", Text("alpha")))
                                           .AddBlock(RdaArchiveBuilder.Deleted, ("data/gone.txt", []), ("data/also-gone.txt", []))
                                           .AddBlock(0, ("data/b.txt", Text("beta")))
                                           .Build();

        // Act
        using var archive = OpenArchive(built.Bytes);

        // Assert
        Assert.AreSequenceEqual(["data/a.txt", "data/b.txt"], archive.Entries.Select(entry => entry.Path).ToArray(), "The entries of a deleted block should not be listed");
        Assert.AreEqual(0, archive.EncryptedBlockCount, "A deleted block is not an encrypted one");
    }

    /// <summary>
    /// A block without files adds no entries and leaves the chain readable.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void RdaArchiveOpenBlockWithoutFilesAddsNoEntries()
    {
        // Arrange
        var built = new RdaArchiveBuilder().AddBlock(0, ("data/a.txt", Text("alpha")))
                                           .AddBlock(0)
                                           .AddBlock(RdaArchiveBuilder.Compressed)
                                           .AddBlock(0, ("data/b.txt", Text("beta")))
                                           .Build();

        // Act
        using var archive = OpenArchive(built.Bytes);

        // Assert
        Assert.AreSequenceEqual(["data/a.txt", "data/b.txt"], archive.Entries.Select(entry => entry.Path).ToArray(), "Only the entries of the blocks with files should be listed");
    }

    /// <summary>
    /// Opening a path reads the file on disk.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void RdaArchiveOpenPathOpensFileOnDisk()
    {
        // Arrange
        var directory = CreateTemporaryDirectory();

        try
        {
            var path = Path.Combine(directory, "synthetic.rda");

            File.WriteAllBytes(path, new RdaArchiveBuilder().AddBlock(0, ("data/a.txt", Text("alpha"))).Build().Bytes);

            // Act
            var result = RdaArchive.Open(path, RdaLimits.Default);

            // Assert
            Assert.IsNull(result.Error, "A readable archive file should open");
            Assert.IsNotNull(result.Archive, "A readable archive file should give an archive");

            using var archive = result.Archive;

            AssertContent(Text("alpha"), archive.Read("data/a.txt"));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    #endregion // AC3

    #region AC4 Open

    /// <summary>
    /// A file that does not start with the magic is not an archive.
    /// </summary>
    [TestMethod]
    [TestCategory("AC4")]
    public void RdaArchiveOpenWrongMagicReturnsUnsupportedArchive()
    {
        // Arrange
        var bytes = StoredArchive().Bytes;

        Encoding.ASCII.GetBytes("Not an RDA archive").CopyTo(bytes, 0);

        // Act
        var result = RdaArchive.Open(new MemoryStream(bytes), RdaLimits.Default);

        // Assert
        AssertOpenError(RdaErrorKind.UnsupportedArchive, result);
    }

    /// <summary>
    /// Another version of the format, also the UTF-16 one of version 2.0, is not supported.
    /// </summary>
    /// <param name="magic">The magic text</param>
    /// <param name="codePage">The code page of the magic</param>
    [TestMethod]
    [TestCategory("AC4")]
    [DataRow("Resource File V2.0", 1200)]
    [DataRow("Resource File V2.1", 20127)]
    [DataRow("Resource File V2.3", 20127)]
    public void RdaArchiveOpenOtherVersionReturnsUnsupportedArchive(string magic, int codePage)
    {
        // Arrange
        var bytes = StoredArchive().Bytes;

        Encoding.GetEncoding(codePage).GetBytes(magic).CopyTo(bytes, 0);

        // Act
        var result = RdaArchive.Open(new MemoryStream(bytes), RdaLimits.Default);

        // Assert
        AssertOpenError(RdaErrorKind.UnsupportedArchive, result);
    }

    /// <summary>
    /// A file shorter than the header region is corrupt.
    /// </summary>
    [TestMethod]
    [TestCategory("AC4")]
    public void RdaArchiveOpenTruncatedHeaderReturnsCorruptArchive()
    {
        // Arrange
        var bytes = StoredArchive().Bytes.Take(400).ToArray();

        // Act
        var result = RdaArchive.Open(new MemoryStream(bytes), RdaLimits.Default);

        // Assert
        AssertOpenError(RdaErrorKind.CorruptArchive, result);
    }

    /// <summary>
    /// A first block offset whose header does not lie inside the file is corrupt.
    /// </summary>
    /// <param name="delta">The distance of the offset from the file length</param>
    [TestMethod]
    [TestCategory("AC4")]
    [DataRow(-16)]
    [DataRow(1000)]
    public void RdaArchiveOpenBlockOutsideFileReturnsCorruptArchive(int delta)
    {
        // Arrange
        var bytes = StoredArchive().Bytes;

        RdaArchiveBuilder.WriteUInt64(bytes, RdaArchiveBuilder.FirstBlockOffsetPosition, (ulong)(bytes.Length + delta));

        // Act
        var result = RdaArchive.Open(new MemoryStream(bytes), RdaLimits.Default);

        // Assert
        AssertOpenError(RdaErrorKind.CorruptArchive, result);
    }

    /// <summary>
    /// A 64-bit value above <see cref="long.MaxValue" /> in the header region or a block header is corrupt.
    /// </summary>
    /// <param name="originIndex">Whether the position counts from the file start (0) or the block header (1)</param>
    /// <param name="position">The position of the value</param>
    [TestMethod]
    [TestCategory("AC4")]
    [DataRow(0, RdaArchiveBuilder.FirstBlockOffsetPosition)]
    [DataRow(1, RdaArchiveBuilder.BlockStoredSizePosition)]
    [DataRow(1, RdaArchiveBuilder.BlockDirectorySizePosition)]
    [DataRow(1, RdaArchiveBuilder.BlockNextPosition)]
    public void RdaArchiveOpenBlockHeaderValueAboveInt64MaxReturnsCorruptArchive(int originIndex, int position)
    {
        // Arrange
        var built = StoredArchive();
        var origin = new[] { 0L, built.Blocks[0].HeaderOffset }[originIndex];

        RdaArchiveBuilder.WriteUInt64(built.Bytes, origin + position, AboveInt64Max);

        // Act
        var result = RdaArchive.Open(new MemoryStream(built.Bytes), RdaLimits.Default);

        // Assert
        AssertOpenError(RdaErrorKind.CorruptArchive, result);
    }

    /// <summary>
    /// A directory that reaches before the start of the file is corrupt.
    /// </summary>
    [TestMethod]
    [TestCategory("AC4")]
    public void RdaArchiveOpenDirectoryOutsideFileReturnsCorruptArchive()
    {
        // Arrange
        var built = StoredArchive();
        var header = built.Blocks[0].HeaderOffset;

        RdaArchiveBuilder.WriteUInt32(built.Bytes, header + RdaArchiveBuilder.BlockFileCountPosition, 100);
        RdaArchiveBuilder.WriteUInt64(built.Bytes, header + RdaArchiveBuilder.BlockStoredSizePosition, 100 * RdaArchiveBuilder.EntrySize);
        RdaArchiveBuilder.WriteUInt64(built.Bytes, header + RdaArchiveBuilder.BlockDirectorySizePosition, 100 * RdaArchiveBuilder.EntrySize);

        // Act
        var result = RdaArchive.Open(new MemoryStream(built.Bytes), RdaLimits.Default);

        // Assert
        AssertOpenError(RdaErrorKind.CorruptArchive, result);
    }

    /// <summary>
    /// A next block offset that points back into the chain is corrupt, also when it points at the block itself.
    /// </summary>
    /// <param name="patchedBlock">The block whose next offset is changed</param>
    /// <param name="targetBlock">The block it points to</param>
    [TestMethod]
    [TestCategory("AC4")]
    [DataRow(1, 0)]
    [DataRow(0, 0)]
    public void RdaArchiveOpenRevisitedBlockOffsetReturnsCorruptArchive(int patchedBlock, int targetBlock)
    {
        // Arrange
        var built = new RdaArchiveBuilder().AddBlock(0, ("data/a.txt", Text("alpha"))).AddBlock(0, ("data/b.txt", Text("beta"))).Build();

        RdaArchiveBuilder.WriteUInt64(built.Bytes, built.Blocks[patchedBlock].HeaderOffset + RdaArchiveBuilder.BlockNextPosition, (ulong)built.Blocks[targetBlock].HeaderOffset);

        // Act
        var result = RdaArchive.Open(new MemoryStream(built.Bytes), RdaLimits.Default);

        // Assert
        AssertOpenError(RdaErrorKind.CorruptArchive, result);
    }

    /// <summary>
    /// More blocks than the limit, empty ones included, are corrupt.
    /// </summary>
    [TestMethod]
    [TestCategory("AC4")]
    public void RdaArchiveOpenTooManyBlocksReturnsCorruptArchive()
    {
        // Arrange
        var bytes = new RdaArchiveBuilder().AddBlock(0).AddBlock(0).AddBlock(0).AddBlock(0).Build().Bytes;

        // Act
        var result = RdaArchive.Open(new MemoryStream(bytes), new RdaLimits(3, 100, 100_000, 1000));

        // Assert
        AssertOpenError(RdaErrorKind.CorruptArchive, result);
    }

    /// <summary>
    /// A running entry total above the limit is corrupt, although each block alone stays below it.
    /// </summary>
    [TestMethod]
    [TestCategory("AC4")]
    public void RdaArchiveOpenTooManyEntriesReturnsCorruptArchive()
    {
        // Arrange
        var bytes = new RdaArchiveBuilder().AddBlock(0, ("data/a.txt", Text("a")), ("data/b.txt", Text("b")))
                                           .AddBlock(0, ("data/c.txt", Text("c")), ("data/d.txt", Text("d")))
                                           .Build()
                                           .Bytes;

        // Act
        var result = RdaArchive.Open(new MemoryStream(bytes), new RdaLimits(100, 3, 100_000, 1000));

        // Assert
        AssertOpenError(RdaErrorKind.CorruptArchive, result);
    }

    /// <summary>
    /// A file count whose directory size wraps around in 32 bits is corrupt: the size is computed in 64 bits.
    /// </summary>
    [TestMethod]
    [TestCategory("AC4")]
    public void RdaArchiveOpenFileCountOverflowReturnsCorruptArchive()
    {
        // Arrange
        var built = StoredArchive();

        // 268,435,457 x 560 wraps to 560 in 32 bits, the size of the one directory entry that is there
        RdaArchiveBuilder.WriteUInt32(built.Bytes, built.Blocks[0].HeaderOffset + RdaArchiveBuilder.BlockFileCountPosition, 268_435_457);

        // Act
        var result = RdaArchive.Open(new MemoryStream(built.Bytes), new RdaLimits(1000, int.MaxValue, int.MaxValue, 1000));

        // Assert
        AssertOpenError(RdaErrorKind.CorruptArchive, result);
    }

    /// <summary>
    /// A directory above the limit is corrupt.
    /// </summary>
    [TestMethod]
    [TestCategory("AC4")]
    public void RdaArchiveOpenDirectoryAboveBoundReturnsCorruptArchive()
    {
        // Arrange
        var bytes = new RdaArchiveBuilder().AddBlock(0, ("data/a.txt", Text("a")), ("data/b.txt", Text("b"))).Build().Bytes;

        // Act
        var result = RdaArchive.Open(new MemoryStream(bytes), new RdaLimits(100, 100, 1000, 1000));

        // Assert
        AssertOpenError(RdaErrorKind.CorruptArchive, result);
    }

    /// <summary>
    /// A declared directory size that does not match the file count is corrupt, stored or compressed.
    /// </summary>
    /// <param name="flags">The flags of the block</param>
    [TestMethod]
    [TestCategory("AC4")]
    [DataRow(0u)]
    [DataRow(1u)]
    public void RdaArchiveOpenDirectorySizeMismatchReturnsCorruptArchive(uint flags)
    {
        // Arrange
        var built = new RdaArchiveBuilder().AddBlock(flags, ("data/a.txt", Text("alpha"))).Build();

        RdaArchiveBuilder.WriteUInt64(built.Bytes, built.Blocks[0].HeaderOffset + RdaArchiveBuilder.BlockDirectorySizePosition, 2 * RdaArchiveBuilder.EntrySize);

        // Act
        var result = RdaArchive.Open(new MemoryStream(built.Bytes), RdaLimits.Default);

        // Assert
        AssertOpenError(RdaErrorKind.CorruptArchive, result);
    }

    /// <summary>
    /// A compressed directory that ends early, so that it yields fewer bytes than declared, is corrupt.
    /// </summary>
    [TestMethod]
    [TestCategory("AC4")]
    public void RdaArchiveOpenTruncatedCompressedDirectoryReturnsCorruptArchive()
    {
        // Arrange
        var compressed = RdaArchiveBuilder.Deflate(RdaArchiveBuilder.DirectoryOf("data/a.txt", "data/b.txt"));
        var bytes = new RdaArchiveBuilder().AddBlockWithDirectory(RdaArchiveBuilder.Compressed, compressed.Take(compressed.Length / 2).ToArray(), ("data/a.txt", Text("alpha")), ("data/b.txt", Text("beta")))
                                           .Build()
                                           .Bytes;

        // Act
        var result = RdaArchive.Open(new MemoryStream(bytes), RdaLimits.Default);

        // Assert
        AssertOpenError(RdaErrorKind.CorruptArchive, result);
    }

    /// <summary>
    /// A compressed directory that is no valid zlib stream is corrupt.
    /// </summary>
    [TestMethod]
    [TestCategory("AC4")]
    public void RdaArchiveOpenCorruptCompressedDirectoryReturnsCorruptArchive()
    {
        // Arrange
        var bytes = new RdaArchiveBuilder().AddBlockWithDirectory(RdaArchiveBuilder.Compressed, Enumerable.Repeat((byte)0xFF, 24).ToArray(), ("data/a.txt", Text("alpha")))
                                           .Build()
                                           .Bytes;

        // Act
        var result = RdaArchive.Open(new MemoryStream(bytes), RdaLimits.Default);

        // Assert
        AssertOpenError(RdaErrorKind.CorruptArchive, result);
    }

    /// <summary>
    /// Blocks whose stored directories overlap, so that together they exceed the file, are corrupt.
    /// </summary>
    [TestMethod]
    [TestCategory("AC4")]
    public void RdaArchiveOpenOverlappingDirectoriesAboveTotalBoundReturnsCorruptArchive()
    {
        // Arrange
        const int directoryBytes = 2000;
        var region = new byte[directoryBytes];

        RdaArchiveBuilder.Deflate([]).CopyTo(region, 0);

        var first = (ulong)(RdaArchiveBuilder.HeaderSize + directoryBytes);
        var bytes = new[]
                    {
                        RdaArchiveBuilder.Header(first),
                        region,
                        RdaArchiveBuilder.BlockHeader(RdaArchiveBuilder.Compressed, 0, directoryBytes, 0, first + 32),
                        RdaArchiveBuilder.BlockHeader(RdaArchiveBuilder.Compressed, 0, directoryBytes, 0, first + 64),
                        RdaArchiveBuilder.BlockHeader(RdaArchiveBuilder.Compressed, 0, directoryBytes, 0, first + 96)
                    }.SelectMany(part => part)
                     .ToArray();

        // Act
        var result = RdaArchive.Open(new MemoryStream(bytes), RdaLimits.Default);

        // Assert
        AssertOpenError(RdaErrorKind.CorruptArchive, result);
    }

    /// <summary>
    /// An encrypted block is counted, adds no entries and leaves the other entries readable.
    /// </summary>
    [TestMethod]
    [TestCategory("AC4")]
    public void RdaArchiveOpenEncryptedBlockCountsItAndOthersRead()
    {
        // Arrange
        var built = new RdaArchiveBuilder().AddBlock(RdaArchiveBuilder.Encrypted, ("data/secret.bin", Text("sealed")))
                                           .AddBlock(0, ("data/open.txt", Text("open")))
                                           .Build();

        // Act
        using var archive = OpenArchive(built.Bytes);
        var result = archive.Read("data/open.txt");

        // Assert
        Assert.AreEqual(1, archive.EncryptedBlockCount, "The encrypted block should be counted");
        Assert.AreSequenceEqual(["data/open.txt"], archive.Entries.Select(entry => entry.Path).ToArray(), "The encrypted block should add no entries");
        AssertContent(Text("open"), result);
    }

    /// <summary>
    /// A file that does not exist is unreadable, whether or not its folder exists.
    /// </summary>
    /// <param name="folder">A folder below the temporary folder that does not exist, or empty</param>
    [TestMethod]
    [TestCategory("AC4")]
    [DataRow("")]
    [DataRow("censio-rda-missing-folder")]
    public void RdaArchiveOpenMissingFileReturnsUnreadable(string folder)
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), folder, Guid.NewGuid().ToString("N") + ".rda");

        // Act
        var result = RdaArchive.Open(path, RdaLimits.Default);

        // Assert
        AssertOpenError(RdaErrorKind.Unreadable, result);
    }

    #endregion // AC4 Open

    #region AC4 Read

    /// <summary>
    /// An entry whose data lies partly or wholly outside the file is corrupt.
    /// </summary>
    /// <param name="delta">The distance of the data offset from the file length</param>
    [TestMethod]
    [TestCategory("AC4")]
    [DataRow(-2)]
    [DataRow(10_000)]
    public void RdaArchiveReadEntryOutsideFileReturnsCorruptEntry(int delta)
    {
        // Arrange
        var built = StoredArchive();

        RdaArchiveBuilder.WriteUInt64(built.Bytes, built.Blocks[0].EntryPosition(0) + RdaArchiveBuilder.EntryOffsetPosition, (ulong)(built.Bytes.Length + delta));

        using var archive = OpenArchive(built.Bytes);

        // Act
        var result = archive.Read("data/a.txt");

        // Assert
        AssertReadError(RdaErrorKind.CorruptEntry, result);
    }

    /// <summary>
    /// An entry whose size or stored size is above <see cref="long.MaxValue" /> is corrupt.
    /// </summary>
    /// <param name="position">The position of the value in the directory entry</param>
    [TestMethod]
    [TestCategory("AC4")]
    [DataRow(RdaArchiveBuilder.EntryStoredSizePosition)]
    [DataRow(RdaArchiveBuilder.EntrySizePosition)]
    public void RdaArchiveReadEntrySizeAboveInt64MaxReturnsCorruptEntry(int position)
    {
        // Arrange
        var built = StoredArchive();

        RdaArchiveBuilder.WriteUInt64(built.Bytes, built.Blocks[0].EntryPosition(0) + position, AboveInt64Max);

        using var archive = OpenArchive(built.Bytes);

        // Act
        var result = archive.Read("data/a.txt");

        // Assert
        AssertReadError(RdaErrorKind.CorruptEntry, result);
    }

    /// <summary>
    /// An entry whose offset plus size overflows a signed 64-bit value is corrupt.
    /// </summary>
    [TestMethod]
    [TestCategory("AC4")]
    public void RdaArchiveReadEntryOffsetPlusSizeOverflowReturnsCorruptEntry()
    {
        // Arrange
        var built = StoredArchive();

        RdaArchiveBuilder.WriteUInt64(built.Bytes, built.Blocks[0].EntryPosition(0) + RdaArchiveBuilder.EntryOffsetPosition, long.MaxValue - 2);

        using var archive = OpenArchive(built.Bytes);

        // Act
        var result = archive.Read("data/a.txt");

        // Assert
        AssertReadError(RdaErrorKind.CorruptEntry, result);
    }

    /// <summary>
    /// An entry larger than the entry limit is corrupt.
    /// </summary>
    [TestMethod]
    [TestCategory("AC4")]
    public void RdaArchiveReadEntryAboveBoundReturnsCorruptEntry()
    {
        // Arrange
        var built = new RdaArchiveBuilder().AddBlock(0, ("data/big.bin", Text("x", 200))).Build();
        using var archive = OpenArchive(built.Bytes, new RdaLimits(100, 100, 100_000, 100));

        // Act
        var result = archive.Read("data/big.bin");

        // Assert
        AssertReadError(RdaErrorKind.CorruptEntry, result);
    }

    /// <summary>
    /// A compressed entry whose stored size is above the entry limit is corrupt, although its size is not.
    /// </summary>
    [TestMethod]
    [TestCategory("AC4")]
    public void RdaArchiveReadCompressedStoredSizeAboveBoundReturnsCorruptEntry()
    {
        // Arrange
        var built = new RdaArchiveBuilder().AddBlockEditingEntries(RdaArchiveBuilder.Compressed, entry => RdaArchiveBuilder.WriteUInt64(entry, RdaArchiveBuilder.EntryStoredSizePosition, 1000), ("data/a.txt", Text("x", 50))).Build();
        using var archive = OpenArchive(built.Bytes, new RdaLimits(100, 100, 100_000, 100));

        // Act
        var result = archive.Read("data/a.txt");

        // Assert
        AssertReadError(RdaErrorKind.CorruptEntry, result);
    }

    /// <summary>
    /// A compressed entry that yields fewer bytes than declared is corrupt.
    /// </summary>
    [TestMethod]
    [TestCategory("AC4")]
    public void RdaArchiveReadDecompressedShorterThanDeclaredReturnsCorruptEntry()
    {
        // Arrange
        var built = new RdaArchiveBuilder().AddBlockEditingEntries(RdaArchiveBuilder.Compressed, entry => RdaArchiveBuilder.WriteUInt64(entry, RdaArchiveBuilder.EntrySizePosition, 80), ("data/a.txt", Text("x", 50))).Build();
        using var archive = OpenArchive(built.Bytes);

        // Act
        var result = archive.Read("data/a.txt");

        // Assert
        AssertReadError(RdaErrorKind.CorruptEntry, result);
    }

    /// <summary>
    /// A compressed entry that yields more bytes than declared is corrupt.
    /// </summary>
    [TestMethod]
    [TestCategory("AC4")]
    public void RdaArchiveReadDecompressedLongerThanDeclaredReturnsCorruptEntry()
    {
        // Arrange
        var built = new RdaArchiveBuilder().AddBlockEditingEntries(RdaArchiveBuilder.Compressed, entry => RdaArchiveBuilder.WriteUInt64(entry, RdaArchiveBuilder.EntrySizePosition, 30), ("data/a.txt", Text("x", 50))).Build();
        using var archive = OpenArchive(built.Bytes);

        // Act
        var result = archive.Read("data/a.txt");

        // Assert
        AssertReadError(RdaErrorKind.CorruptEntry, result);
    }

    /// <summary>
    /// A compressed entry that is no valid zlib stream is corrupt.
    /// </summary>
    [TestMethod]
    [TestCategory("AC4")]
    public void RdaArchiveReadCorruptCompressedEntryReturnsCorruptEntry()
    {
        // Arrange
        var built = new RdaArchiveBuilder().AddBlock(RdaArchiveBuilder.Compressed, ("data/a.txt", Text("x", 50))).Build();

        Array.Fill(built.Bytes, (byte)0xFF, (int)built.Blocks[0].DataOffsets[0], 4);

        using var archive = OpenArchive(built.Bytes);

        // Act
        var result = archive.Read("data/a.txt");

        // Assert
        AssertReadError(RdaErrorKind.CorruptEntry, result);
    }

    /// <summary>
    /// A memory-resident block whose decompressed data size is above the entry limit is corrupt.
    /// </summary>
    [TestMethod]
    [TestCategory("AC4")]
    public void RdaArchiveReadMemoryResidentDataAboveBoundReturnsCorruptEntry()
    {
        // Arrange
        var built = new RdaArchiveBuilder().AddBlock(RdaArchiveBuilder.MemoryResident, ("data/a.txt", Text("x", 50))).Build();

        RdaArchiveBuilder.WriteUInt64(built.Bytes, built.Blocks[0].DirectoryOffset - 8, 5000);

        using var archive = OpenArchive(built.Bytes, new RdaLimits(100, 100, 100_000, 1000));

        // Act
        var result = archive.Read("data/a.txt");

        // Assert
        AssertReadError(RdaErrorKind.CorruptEntry, result);
    }

    /// <summary>
    /// A memory-resident block whose stored data size is above the entry limit is corrupt.
    /// </summary>
    [TestMethod]
    [TestCategory("AC4")]
    public void RdaArchiveReadMemoryResidentStoredSizeAboveBoundReturnsCorruptEntry()
    {
        // Arrange
        var built = new RdaArchiveBuilder().AddBlock(RdaArchiveBuilder.MemoryResident, ("data/a.txt", Text("x", 50))).Build();

        RdaArchiveBuilder.WriteUInt64(built.Bytes, built.Blocks[0].DirectoryOffset - 16, 5000);

        using var archive = OpenArchive(built.Bytes, new RdaLimits(100, 100, 100_000, 1000));

        // Act
        var result = archive.Read("data/a.txt");

        // Assert
        AssertReadError(RdaErrorKind.CorruptEntry, result);
    }

    /// <summary>
    /// An entry of a memory-resident block whose slice offset plus size overflows is corrupt.
    /// </summary>
    [TestMethod]
    [TestCategory("AC4")]
    public void RdaArchiveReadMemoryResidentSliceOverflowReturnsCorruptEntry()
    {
        // Arrange
        var built = new RdaArchiveBuilder().AddBlock(RdaArchiveBuilder.MemoryResident, ("data/a.txt", Text("x", 50))).Build();

        RdaArchiveBuilder.WriteUInt64(built.Bytes, built.Blocks[0].EntryPosition(0) + RdaArchiveBuilder.EntryOffsetPosition, long.MaxValue - 2);

        using var archive = OpenArchive(built.Bytes);

        // Act
        var result = archive.Read("data/a.txt");

        // Assert
        AssertReadError(RdaErrorKind.CorruptEntry, result);
    }

    /// <summary>
    /// An entry of a block with a flag the reader does not know is unsupported while the others stay readable.
    /// </summary>
    [TestMethod]
    [TestCategory("AC4")]
    public void RdaArchiveReadUnknownFlagReturnsUnsupportedEntryAndOthersRead()
    {
        // Arrange
        var built = new RdaArchiveBuilder().AddBlock(RdaArchiveBuilder.Unknown, ("data/odd.bin", Text("odd")))
                                           .AddBlock(0, ("data/plain.txt", Text("plain")))
                                           .Build();
        using var archive = OpenArchive(built.Bytes);

        // Act
        string[] paths = ["data/odd.bin", "data/plain.txt"];
        var results = paths.Select(archive.Read).ToArray();

        // Assert
        AssertReadError(RdaErrorKind.UnsupportedEntry, results[0]);
        AssertContent(Text("plain"), results[1]);
        Assert.AreEqual((RdaBlockAttributes)RdaArchiveBuilder.Unknown, archive.Entries[0].Flags, "The unknown flag should be kept on the entry");
    }

    /// <summary>
    /// A path that is not in the archive, also in another letter case, is not found.
    /// </summary>
    /// <param name="path">The path to read</param>
    [TestMethod]
    [TestCategory("AC4")]
    [DataRow("data/missing.txt")]
    [DataRow("DATA/A.TXT")]
    public void RdaArchiveReadMissingPathReturnsNotFound(string path)
    {
        // Arrange
        using var archive = OpenArchive(StoredArchive().Bytes);

        // Act
        var result = archive.Read(path);

        // Assert
        AssertReadError(RdaErrorKind.NotFound, result);
    }

    /// <summary>
    /// A path listed twice is listed twice and read from neither entry.
    /// </summary>
    [TestMethod]
    [TestCategory("AC4")]
    public void RdaArchiveReadDuplicatePathReturnsDuplicateEntry()
    {
        // Arrange
        var built = new RdaArchiveBuilder().AddBlock(0, ("data/dup.txt", Text("one")), ("data/other.txt", Text("other")))
                                           .AddBlock(0, ("data/dup.txt", Text("two")))
                                           .Build();
        using var archive = OpenArchive(built.Bytes);

        // Act
        string[] paths = ["data/dup.txt", "data/other.txt"];
        var results = paths.Select(archive.Read).ToArray();

        // Assert
        Assert.HasCount(3, archive.Entries, "Both copies of the path should be listed");
        AssertReadError(RdaErrorKind.DuplicateEntry, results[0]);
        AssertContent(Text("other"), results[1]);
    }

    /// <summary>
    /// A stream that fails while an entry is read makes the read unreadable.
    /// </summary>
    [TestMethod]
    [TestCategory("AC4")]
    public void RdaArchiveReadStreamFailureReturnsUnreadable()
    {
        // Arrange
        const long dataOffset = 2_000_000;
        var content = Text("invented content");
        var directoryOffset = dataOffset + content.Length;
        var entry = RdaArchiveBuilder.DirectoryEntry("data/a.txt", dataOffset, (ulong)content.Length, (ulong)content.Length);
        var headerOffset = directoryOffset + entry.Length;
        var length = headerOffset + RdaArchiveBuilder.BlockHeaderSize;
        var stream = new SparseStream(length).WithRegion(0, RdaArchiveBuilder.Header((ulong)headerOffset))
                                             .WithRegion(dataOffset, content)
                                             .WithRegion(directoryOffset, entry)
                                             .WithRegion(headerOffset, RdaArchiveBuilder.BlockHeader(0, 1, RdaArchiveBuilder.EntrySize, RdaArchiveBuilder.EntrySize, (ulong)length))
                                             .ThrowingAt(dataOffset);
        using var archive = OpenArchive(stream);

        // Act
        var result = archive.Read("data/a.txt");

        // Assert
        AssertReadError(RdaErrorKind.Unreadable, result);
    }

    /// <summary>
    /// An uncompressed directory whose stored size differs from its declared size, which fits the file count, is corrupt.
    /// </summary>
    [TestMethod]
    [TestCategory("AC4")]
    public void RdaArchiveOpenUncompressedDirectoryStoredSizeMismatchReturnsCorruptArchive()
    {
        // Arrange
        var built = StoredArchive();

        RdaArchiveBuilder.WriteUInt64(built.Bytes, built.Blocks[0].HeaderOffset + RdaArchiveBuilder.BlockStoredSizePosition, RdaArchiveBuilder.EntrySize + 10);

        // Act
        var result = RdaArchive.Open(new MemoryStream(built.Bytes), RdaLimits.Default);

        // Assert
        AssertOpenError(RdaErrorKind.CorruptArchive, result);
    }

    /// <summary>
    /// A stream that fails inside the header region or inside the block chain makes the open unreadable.
    /// </summary>
    /// <param name="inChain">0 to fail inside the header region, 1 to fail inside the first block header</param>
    [TestMethod]
    [TestCategory("AC4")]
    [DataRow(0)]
    [DataRow(1)]
    public void RdaArchiveOpenStreamFailureReturnsUnreadable(int inChain)
    {
        // Arrange
        var built = StoredArchive();
        var failAt = new[] { 10L, built.Blocks[0].HeaderOffset + 4 }[inChain];
        var stream = new SparseStream(built.Bytes.Length).WithRegion(0, built.Bytes).ThrowingAt(failAt);

        // Act
        var result = RdaArchive.Open(stream, RdaLimits.Default);

        // Assert
        AssertOpenError(RdaErrorKind.Unreadable, result);
    }

    /// <summary>
    /// A stream that cannot be read or sought is unreadable.
    /// </summary>
    [TestMethod]
    [TestCategory("AC4")]
    public void RdaArchiveOpenNonSeekableStreamReturnsUnreadable()
    {
        // Arrange
        var stream = new System.IO.Compression.GZipStream(new MemoryStream(), System.IO.Compression.CompressionMode.Compress);

        // Act
        var result = RdaArchive.Open(stream, RdaLimits.Default);

        // Assert
        AssertOpenError(RdaErrorKind.Unreadable, result);
    }

    /// <summary>
    /// A stored entry whose stored size differs from its size is corrupt.
    /// </summary>
    [TestMethod]
    [TestCategory("AC4")]
    public void RdaArchiveReadStoredEntrySizeMismatchReturnsCorruptEntry()
    {
        // Arrange
        var built = StoredArchive();

        RdaArchiveBuilder.WriteUInt64(built.Bytes, built.Blocks[0].EntryPosition(0) + RdaArchiveBuilder.EntryStoredSizePosition, 30);

        using var archive = OpenArchive(built.Bytes);

        // Act
        var result = archive.Read("data/a.txt");

        // Assert
        AssertReadError(RdaErrorKind.CorruptEntry, result);
    }

    /// <summary>
    /// A memory-resident block whose directory lies too close to the header region for its two size fields is corrupt.
    /// </summary>
    [TestMethod]
    [TestCategory("AC4")]
    public void RdaArchiveReadMemoryResidentDirectoryTooCloseToHeaderReturnsCorruptEntry()
    {
        // Arrange
        var headerOffset = RdaArchiveBuilder.HeaderSize + RdaArchiveBuilder.EntrySize;
        var bytes = new[]
                    {
                        RdaArchiveBuilder.Header((ulong)headerOffset),
                        RdaArchiveBuilder.DirectoryEntry("data/a.txt", 0, 5, 5),
                        RdaArchiveBuilder.BlockHeader(RdaArchiveBuilder.MemoryResident, 1, RdaArchiveBuilder.EntrySize, RdaArchiveBuilder.EntrySize, (ulong)(headerOffset + RdaArchiveBuilder.BlockHeaderSize))
                    }.SelectMany(part => part)
                     .ToArray();
        using var archive = OpenArchive(bytes);

        // Act
        var result = archive.Read("data/a.txt");

        // Assert
        AssertReadError(RdaErrorKind.CorruptEntry, result);
    }

    /// <summary>
    /// A memory-resident block whose data would start before the end of the header region is corrupt.
    /// </summary>
    [TestMethod]
    [TestCategory("AC4")]
    public void RdaArchiveReadMemoryResidentDataBeforeHeaderRegionReturnsCorruptEntry()
    {
        // Arrange
        var built = new RdaArchiveBuilder().AddBlock(RdaArchiveBuilder.MemoryResident, ("data/a.txt", Text("x", 20))).Build();

        RdaArchiveBuilder.WriteUInt64(built.Bytes, built.Blocks[0].DirectoryOffset - 16, 100);

        using var archive = OpenArchive(built.Bytes);

        // Act
        var result = archive.Read("data/a.txt");

        // Assert
        AssertReadError(RdaErrorKind.CorruptEntry, result);
    }

    /// <summary>
    /// An uncompressed memory-resident block whose stored and decompressed data sizes differ is corrupt.
    /// </summary>
    [TestMethod]
    [TestCategory("AC4")]
    public void RdaArchiveReadMemoryResidentSizesDifferWithoutCompressionReturnsCorruptEntry()
    {
        // Arrange
        var built = new RdaArchiveBuilder().AddBlock(RdaArchiveBuilder.MemoryResident, ("data/a.txt", Text("x", 20))).Build();

        RdaArchiveBuilder.WriteUInt64(built.Bytes, built.Blocks[0].DirectoryOffset - 8, 21);

        using var archive = OpenArchive(built.Bytes);

        // Act
        var result = archive.Read("data/a.txt");

        // Assert
        AssertReadError(RdaErrorKind.CorruptEntry, result);
    }

    /// <summary>
    /// A compressed memory-resident block whose data is no valid zlib stream is corrupt.
    /// </summary>
    [TestMethod]
    [TestCategory("AC4")]
    public void RdaArchiveReadCorruptCompressedMemoryResidentDataReturnsCorruptEntry()
    {
        // Arrange
        var built = new RdaArchiveBuilder().AddBlock(RdaArchiveBuilder.MemoryResident | RdaArchiveBuilder.Compressed, ("data/a.txt", Text("x", 50))).Build();

        Array.Fill(built.Bytes, (byte)0xFF, (int)built.Blocks[0].DataOffsets[0], 4);

        using var archive = OpenArchive(built.Bytes);

        // Act
        var result = archive.Read("data/a.txt");

        // Assert
        AssertReadError(RdaErrorKind.CorruptEntry, result);
    }

    #endregion // AC4 Read

    #region AC5

    /// <summary>
    /// An archive that another process holds open for writing can still be opened and read.
    /// </summary>
    [TestMethod]
    [TestCategory("AC5")]
    public void RdaArchiveOpenWhileFileHeldOpenForWritingSucceeds()
    {
        // Arrange
        var directory = CreateTemporaryDirectory();

        try
        {
            var path = Path.Combine(directory, "synthetic.rda");

            File.WriteAllBytes(path, new RdaArchiveBuilder().AddBlock(0, ("data/a.txt", Text("alpha"))).Build().Bytes);

            using var holder = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete);

            // Act
            var result = RdaArchive.Open(path, RdaLimits.Default);

            // Assert
            Assert.IsNull(result.Error, "An archive held open for writing by the game should still open");
            Assert.IsNotNull(result.Archive, "An archive held open for writing by the game should give an archive");

            using var archive = result.Archive;

            AssertContent(Text("alpha"), archive.Read("data/a.txt"));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    #endregion // AC5

    #region Helpers

    /// <summary>
    /// Creates invented content.
    /// </summary>
    /// <param name="text">The text</param>
    /// <param name="repeat">How often the text is repeated</param>
    /// <returns>The bytes</returns>
    private static byte[] Text(string text, int repeat = 1)
    {
        return RdaArchiveBuilder.Content(text, repeat);
    }

    /// <summary>
    /// Builds an archive of one stored block with one entry, <c>data/a.txt</c>.
    /// </summary>
    /// <returns>The built archive</returns>
    private static RdaBuiltArchive StoredArchive()
    {
        return new RdaArchiveBuilder().AddBlock(0, ("data/a.txt", Text("invented content of the entry"))).Build();
    }

    /// <summary>
    /// Creates an empty folder below the temporary folder.
    /// </summary>
    /// <returns>The path of the folder</returns>
    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "censio-rda-" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(directory);

        return directory;
    }

    /// <summary>
    /// Opens an archive from bytes and expects it to open.
    /// </summary>
    /// <param name="bytes">The bytes of the archive</param>
    /// <param name="limits">The limits, <see cref="RdaLimits.Default" /> when omitted</param>
    /// <returns>The archive</returns>
    private static RdaArchive OpenArchive(byte[] bytes, RdaLimits? limits = null)
    {
        return OpenArchive(new MemoryStream(bytes), limits);
    }

    /// <summary>
    /// Opens an archive from a stream and expects it to open.
    /// </summary>
    /// <param name="stream">The stream of the archive</param>
    /// <param name="limits">The limits, <see cref="RdaLimits.Default" /> when omitted</param>
    /// <returns>The archive</returns>
    private static RdaArchive OpenArchive(Stream stream, RdaLimits? limits = null)
    {
        var result = RdaArchive.Open(stream, limits ?? RdaLimits.Default);

        Assert.IsNull(result.Error, "The synthetic archive should open");

        return result.Archive ?? throw new AssertFailedException("The synthetic archive should give an archive");
    }

    /// <summary>
    /// Asserts that a read gave the expected content.
    /// </summary>
    /// <param name="expected">The expected content</param>
    /// <param name="result">The result of the read</param>
    private static void AssertContent(byte[] expected, RdaReadResult result)
    {
        Assert.IsNull(result.Error, "The entry should be read");
        Assert.AreSequenceEqual(expected, result.Content.ToArray(), "The entry should be read with its decompressed content");
    }

    /// <summary>
    /// Asserts that an open failed with a kind.
    /// </summary>
    /// <param name="kind">The expected kind</param>
    /// <param name="result">The result of the open</param>
    private static void AssertOpenError(RdaErrorKind kind, RdaOpenResult result)
    {
        Assert.IsNull(result.Archive, "A failed open should give no archive");
        Assert.AreEqual(kind, result.Error?.Kind, "The open should fail with the kind of the malformation");
    }

    /// <summary>
    /// Asserts that a read failed with a kind and returned no content.
    /// </summary>
    /// <param name="kind">The expected kind</param>
    /// <param name="result">The result of the read</param>
    private static void AssertReadError(RdaErrorKind kind, RdaReadResult result)
    {
        Assert.AreEqual(kind, result.Error?.Kind, "The read should fail with the kind of the malformation");
        Assert.IsEmpty(result.Content.ToArray(), "A failed read should return no content");
    }

    #endregion // Helpers

    #endregion // Methods
}