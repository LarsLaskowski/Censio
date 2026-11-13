using Censio.GameData;

namespace Censio.Server.Tests;

/// <summary>
/// Tests for <see cref="PngHeader" />.
/// </summary>
[TestClass]
public sealed class PngHeaderTests
{
    #region Constants

    /// <summary>
    /// The first 16 bytes of a PNG file in hex: the signature, the length 13 and the type <c>IHDR</c> of the first chunk.
    /// </summary>
    private const string ValidHeader = "89504E470D0A1A0A0000000D49484452";

    #endregion // Constants

    #region Methods

    /// <summary>
    /// A PNG image the game data's writer produced is a PNG, and so are its first 16 bytes alone.
    /// </summary>
    [TestMethod]
    [TestCategory("AC5")]
    public void PngHeaderAcceptsSignatureAndIhdr()
    {
        // Arrange
        var png = PngWriter.Write(2, 2, new byte[16]);

        // Act
        var isPng = PngHeader.IsPng(png);
        var headerIsPng = PngHeader.IsPng(png.AsSpan(0, 16));

        // Assert
        Assert.AreEqual(ValidHeader, Convert.ToHexString(png.AsSpan(0, 16)), "The written image should start with the header the check expects, or the test would prove less");
        Assert.IsTrue(isPng, "A written PNG image should be accepted");
        Assert.IsTrue(headerIsPng, "The signature followed by the IHDR chunk's length and type should be accepted");
    }

    /// <summary>
    /// Data that is shorter than the header, has another signature, another length of the first chunk, or another first
    /// chunk is refused.
    /// </summary>
    /// <param name="hex">The data in hex</param>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow("")]
    [DataRow("89504E470D0A1A0A")]
    [DataRow("89504E470D0A1A0A0000000D494844")]
    [DataRow("88504E470D0A1A0A0000000D49484452")]
    [DataRow("89504E470D0A1A0B0000000D49484452")]
    [DataRow("89504E470D0A1A0A0000000C49484452")]
    [DataRow("89504E470D0A1A0A0000010D49484452")]
    [DataRow("89504E470D0A1A0A0000000D69686472")]
    [DataRow("89504E470D0A1A0A0000000D49444154")]
    [DataRow("706E6720616263")]
    [DataRow("3C7376672F3E3C7376672F3E3C737667")]
    public void PngHeaderRefusesShortOrWrongBytes(string hex)
    {
        // Arrange
        var data = Convert.FromHexString(hex);

        // Act
        var isPng = PngHeader.IsPng(data);

        // Assert
        Assert.IsFalse(isPng, $"The data {hex} should not be accepted as a PNG");
    }

    #endregion // Methods
}