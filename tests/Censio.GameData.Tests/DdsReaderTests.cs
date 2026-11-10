namespace Censio.GameData.Tests;

/// <summary>
/// Tests for <see cref="DdsReader" />. The files are built in code; no game icon is used.
/// </summary>
[TestClass]
public sealed class DdsReaderTests
{
    #region Constants

    /// <summary>
    /// A bound that no test image exceeds.
    /// </summary>
    private const int Bound = 2048;

    #endregion // Constants

    #region Methods

    /// <summary>
    /// DXGI 98 (UNORM) and 99 (sRGB) give the same bytes, with no colour conversion.
    /// </summary>
    /// <param name="dxgiFormat">The DXGI format</param>
    [TestMethod]
    [TestCategory("AC1")]
    [DataRow(DdsFileBuilder.DxgiBc7Unorm)]
    [DataRow(DdsFileBuilder.DxgiBc7Srgb)]
    public void DdsReaderReadSrgbAndUnormGiveSameBytes(int dxgiFormat)
    {
        // Arrange
        var file = DdsFileBuilder.Build(8, 4, dxgiFormat, 1);

        // Act
        var image = DdsReader.Read(file, Bound);

        // Assert
        Assert.IsNotNull(image, "A BC7 file should be read");
        Assert.AreEqual(8, image.Width, "The width should be the file's");
        Assert.AreEqual(4, image.Height, "The height should be the file's");
        Assert.AreEqual(Convert.ToHexString(DdsFileBuilder.ExpectedRgba(8, 4)), Convert.ToHexString(image.Rgba.Span), "Both formats should give the stored values");
    }

    /// <summary>
    /// A size that is no multiple of 4 is decoded from whole blocks and cropped.
    /// </summary>
    /// <param name="width">The width in pixels</param>
    /// <param name="height">The height in pixels</param>
    [TestMethod]
    [TestCategory("AC1")]
    [DataRow(1, 1)]
    [DataRow(5, 3)]
    [DataRow(7, 9)]
    [DataRow(10, 6)]
    [DataRow(13, 2)]
    [DataRow(4, 4)]
    public void DdsReaderReadSizeNotMultipleOfFourIsCropped(int width, int height)
    {
        // Arrange
        var file = DdsFileBuilder.Build(width, height, DdsFileBuilder.DxgiBc7Srgb, 1);

        // Act
        var image = DdsReader.Read(file, Bound);

        // Assert
        Assert.IsNotNull(image, "The file should be read");
        Assert.AreEqual(width, image.Width, "The width should not be rounded up");
        Assert.AreEqual(height, image.Height, "The height should not be rounded up");
        Assert.AreEqual(Convert.ToHexString(DdsFileBuilder.ExpectedRgba(width, height)), Convert.ToHexString(image.Rgba.Span), "The pixels should be the cropped blocks");
    }

    /// <summary>
    /// Only level 0 is decoded, however many levels follow, also when a level is no multiple of 4.
    /// </summary>
    /// <param name="width">The width in pixels</param>
    /// <param name="height">The height in pixels</param>
    /// <param name="levelCount">The number of levels</param>
    [TestMethod]
    [TestCategory("AC1")]
    [DataRow(16, 16, 5)]
    [DataRow(10, 6, 4)]
    [DataRow(8, 8, 2)]
    public void DdsReaderReadUsesLargestLevel(int width, int height, int levelCount)
    {
        // Arrange
        var file = DdsFileBuilder.Build(width, height, DdsFileBuilder.DxgiBc7Srgb, levelCount);

        // Act
        var image = DdsReader.Read(file, Bound);

        // Assert
        Assert.IsNotNull(image, "A file with levels should be read");
        Assert.AreEqual(width, image.Width, "The width should be that of level 0");
        Assert.AreEqual(height, image.Height, "The height should be that of level 0");
        Assert.AreEqual(Convert.ToHexString(DdsFileBuilder.ExpectedRgba(width, height)), Convert.ToHexString(image.Rgba.Span), "The pixels should be those of level 0");
    }

    /// <summary>
    /// With the level flag set, a level count of 0 is read as 1.
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void DdsReaderReadLevelCountZeroWithFlagIsOneLevel()
    {
        // Arrange
        var file = DdsFileBuilder.Patch(DdsFileBuilder.Build(8, 8, DdsFileBuilder.DxgiBc7Srgb, 1), DdsFileBuilder.FlagsPosition, DdsFileBuilder.FlagsWithLevels);

        file = DdsFileBuilder.Patch(file, DdsFileBuilder.LevelCountPosition, 0);

        // Act
        var image = DdsReader.Read(file, Bound);

        // Assert
        Assert.IsNotNull(image, "A level count of 0 should be read as 1");
        Assert.AreEqual(Convert.ToHexString(DdsFileBuilder.ExpectedRgba(8, 8)), Convert.ToHexString(image.Rgba.Span), "The pixels should be those of level 0");
    }

    /// <summary>
    /// Without the level flag, the level count field is ignored and the file holds one level.
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void DdsReaderReadWithoutLevelFlagIgnoresLevelCount()
    {
        // Arrange
        var file = DdsFileBuilder.Patch(DdsFileBuilder.Build(8, 8, DdsFileBuilder.DxgiBc7Srgb, 1), DdsFileBuilder.LevelCountPosition, 5);

        // Act
        var image = DdsReader.Read(file, Bound);

        // Assert
        Assert.IsNotNull(image, "A level count without the flag should be ignored");
        Assert.AreEqual(Convert.ToHexString(DdsFileBuilder.ExpectedRgba(8, 8)), Convert.ToHexString(image.Rgba.Span), "The pixels should be those of level 0");
    }

    /// <summary>
    /// A side equal to the bound is read.
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void DdsReaderReadSideEqualToBoundIsRead()
    {
        // Arrange
        var file = DdsFileBuilder.Build(8, 4, DdsFileBuilder.DxgiBc7Srgb, 1);

        // Act
        var image = DdsReader.Read(file, 8);

        // Assert
        Assert.IsNotNull(image, "A side equal to the bound should be read");
    }

    /// <summary>
    /// A header field the reader does not accept gives <see langword="null" />.
    /// </summary>
    /// <param name="position">The position of the 32-bit field</param>
    /// <param name="value">The value it receives</param>
    [TestMethod]
    [TestCategory("AC3")]
    [DataRow(DdsFileBuilder.MagicPosition, 0x20534445)]
    [DataRow(DdsFileBuilder.HeaderSizePosition, 120)]
    [DataRow(DdsFileBuilder.HeaderSizePosition, 128)]
    [DataRow(DdsFileBuilder.PixelFormatSizePosition, 24)]
    [DataRow(DdsFileBuilder.PixelFormatFlagsPosition, 0x40)]
    [DataRow(DdsFileBuilder.PixelFormatFlagsPosition, 0)]
    [DataRow(DdsFileBuilder.FourCcPosition, 0x35545844)]
    [DataRow(DdsFileBuilder.DxgiFormatPosition, 71)]
    [DataRow(DdsFileBuilder.DxgiFormatPosition, 100)]
    [DataRow(DdsFileBuilder.DxgiFormatPosition, 0)]
    [DataRow(DdsFileBuilder.DimensionPosition, 4)]
    [DataRow(DdsFileBuilder.DimensionPosition, 2)]
    [DataRow(DdsFileBuilder.ArraySizePosition, 2)]
    [DataRow(DdsFileBuilder.ArraySizePosition, 0)]
    [DataRow(DdsFileBuilder.MiscFlagsPosition, 4)]
    [DataRow(DdsFileBuilder.WidthPosition, 0)]
    [DataRow(DdsFileBuilder.HeightPosition, 0)]
    [DataRow(DdsFileBuilder.WidthPosition, -1)]
    [DataRow(DdsFileBuilder.HeightPosition, -1)]
    [DataRow(DdsFileBuilder.WidthPosition, int.MaxValue)]
    [DataRow(DdsFileBuilder.HeightPosition, int.MaxValue)]
    public void DdsReaderReadRejectsMalformedHeader(int position, int value)
    {
        // Arrange
        var file = DdsFileBuilder.Patch(DdsFileBuilder.Build(8, 8, DdsFileBuilder.DxgiBc7Srgb, 1), position, value);

        // Act
        var image = DdsReader.Read(file, Bound);

        // Assert
        Assert.IsNull(image, "A header the reader does not accept should give null");
    }

    /// <summary>
    /// A file cut off inside or right after its headers gives <see langword="null" />.
    /// </summary>
    /// <param name="length">The length of the file prefix</param>
    [TestMethod]
    [TestCategory("AC3")]
    [DataRow(0)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(127)]
    [DataRow(128)]
    [DataRow(147)]
    [DataRow(DdsFileBuilder.HeadersSize)]
    public void DdsReaderReadRejectsTruncatedFile(int length)
    {
        // Arrange
        var file = DdsFileBuilder.Build(8, 8, DdsFileBuilder.DxgiBc7Srgb, 1).AsSpan(0, length).ToArray();

        // Act
        var image = DdsReader.Read(file, Bound);

        // Assert
        Assert.IsNull(image, "A file without its headers or its pixels should give null");
    }

    /// <summary>
    /// A width or height above the bound gives <see langword="null" />, although the file is complete.
    /// </summary>
    /// <param name="width">The width in pixels</param>
    /// <param name="height">The height in pixels</param>
    /// <param name="maxSide">The bound</param>
    [TestMethod]
    [TestCategory("AC3")]
    [DataRow(12, 4, 8)]
    [DataRow(4, 12, 8)]
    [DataRow(8, 8, 7)]
    [DataRow(4, 4, 3)]
    public void DdsReaderReadSideAboveBoundReturnsNull(int width, int height, int maxSide)
    {
        // Arrange
        var file = DdsFileBuilder.Build(width, height, DdsFileBuilder.DxgiBc7Srgb, 1);

        // Act
        var image = DdsReader.Read(file, maxSide);

        // Assert
        Assert.IsNull(image, "A side above the bound should give null");
    }

    /// <summary>
    /// A file one byte too short or too long for its levels gives <see langword="null" />.
    /// </summary>
    /// <param name="levelCount">The number of levels</param>
    /// <param name="delta">The change of the length in bytes</param>
    [TestMethod]
    [TestCategory("AC3")]
    [DataRow(1, -1)]
    [DataRow(1, 1)]
    [DataRow(3, -1)]
    [DataRow(3, 1)]
    public void DdsReaderReadRejectsWrongLength(int levelCount, int delta)
    {
        // Arrange
        var file = DdsFileBuilder.Resize(DdsFileBuilder.Build(8, 8, DdsFileBuilder.DxgiBc7Srgb, levelCount), delta);

        // Act
        var image = DdsReader.Read(file, Bound);

        // Assert
        Assert.IsNull(image, "A length that is not the exact size of the levels should give null");
    }

    /// <summary>
    /// A level count above the full chain gives <see langword="null" />, although the file holds that many levels.
    /// </summary>
    /// <param name="width">The width in pixels</param>
    /// <param name="height">The height in pixels</param>
    /// <param name="levelCount">The number of levels, one above the chain</param>
    [TestMethod]
    [TestCategory("AC3")]
    [DataRow(8, 8, 5)]
    [DataRow(4, 4, 4)]
    [DataRow(1, 1, 2)]
    public void DdsReaderReadLevelCountAboveChainReturnsNull(int width, int height, int levelCount)
    {
        // Arrange
        var file = DdsFileBuilder.Build(width, height, DdsFileBuilder.DxgiBc7Srgb, levelCount);

        // Act
        var image = DdsReader.Read(file, Bound);

        // Assert
        Assert.IsNull(image, "A level count above the full chain should give null");
    }

    #endregion // Methods
}