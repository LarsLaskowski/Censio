namespace Censio.Pipe.Tests;

/// <summary>
/// Tests for <see cref="PipeError" />.
/// </summary>
[TestClass]
public sealed class PipeErrorTests
{
    #region Methods

    /// <summary>
    /// The string form is the kind name followed by the fields that are set, in a fixed order.
    /// </summary>
    /// <param name="kind">The kind</param>
    /// <param name="length">The length, if set</param>
    /// <param name="typeValue">The type value, if set</param>
    /// <param name="offset">The offset, if set</param>
    /// <param name="received">The received version, if set</param>
    /// <param name="expected">The expected version, if set</param>
    /// <param name="expectedText">The expected string form</param>
    [TestMethod]
    [DataRow(PipeErrorKind.EmptyFrame, null, null, null, null, null, "EmptyFrame")]
    [DataRow(PipeErrorKind.OversizedFrame, 1048577, null, null, null, null, "OversizedFrame: length=1048577")]
    [DataRow(PipeErrorKind.MalformedMessage, null, (byte)1, 7, null, null, "MalformedMessage: type=1, offset=7")]
    [DataRow(PipeErrorKind.ProtocolMismatch, null, (byte)3, null, 3, 2, "ProtocolMismatch: type=3, received=3, expected=2")]
    [DataRow(PipeErrorKind.TruncatedFrame, 9, (byte)2, 5, 4, 2, "TruncatedFrame: length=9, type=2, offset=5, received=4, expected=2")]
    public void PipeErrorStringFormListsPresentFields(PipeErrorKind kind, int? length, byte? typeValue, int? offset, int? received, int? expected, string expectedText)
    {
        // Arrange
        var error = new PipeError
                    {
                        Kind = kind,
                        Length = length,
                        TypeValue = typeValue,
                        Offset = offset,
                        ReceivedVersion = received,
                        ExpectedVersion = expected
                    };

        // Act
        var text = error.ToString();

        // Assert
        Assert.AreEqual(expectedText, text, "The string form should list the kind and the set fields in the fixed order");
    }

    #endregion // Methods
}