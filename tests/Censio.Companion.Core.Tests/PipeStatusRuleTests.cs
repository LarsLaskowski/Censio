using Censio.Pipe;

namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for <see cref="PipeStatusRule" />.
/// </summary>
[TestClass]
public sealed class PipeStatusRuleTests
{
    #region Methods

    /// <summary>
    /// A message of the supported protocol means the connection receives.
    /// </summary>
    [TestMethod]
    public void PipeStatusRuleForReadMessageReturnsReceiving()
    {
        // Arrange
        var read = PipeReadResult.FromMessage(new VersionMessage(2));

        // Act
        var status = PipeStatusRule.ForRead(read);

        // Assert
        Assert.AreEqual(new PipeConnectionStatus
                        {
                            State = PipeConnectionState.Receiving
                        },
                        status,
                        "A message should mean the connection receives");
    }

    /// <summary>
    /// Any message type means the connection receives, not only the version.
    /// </summary>
    [TestMethod]
    public void PipeStatusRuleForReadOtherMessageReturnsReceiving()
    {
        // Arrange
        var read = PipeReadResult.FromMessage(new SessionEndMessage());

        // Act
        var status = PipeStatusRule.ForRead(read);

        // Assert
        Assert.AreEqual(new PipeConnectionStatus
                        {
                            State = PipeConnectionState.Receiving
                        },
                        status,
                        "A session end message should mean the connection receives");
    }

    /// <summary>
    /// A frame the reader skips does not change the status.
    /// </summary>
    /// <param name="kind">The kind of the skipped frame's error</param>
    [TestMethod]
    [DataRow(PipeErrorKind.EmptyFrame)]
    [DataRow(PipeErrorKind.UnknownType)]
    [DataRow(PipeErrorKind.MalformedMessage)]
    public void PipeStatusRuleForReadSkippedFrameReturnsNull(PipeErrorKind kind)
    {
        // Arrange
        var read = PipeReadResult.FromError(new PipeError
                                            {
                                                Kind = kind
                                            });

        // Act
        var status = PipeStatusRule.ForRead(read);

        // Assert
        Assert.IsNull(status, "A skipped frame should not change the status");
    }

    /// <summary>
    /// A protocol mismatch carries the received version.
    /// </summary>
    [TestMethod]
    public void PipeStatusRuleForReadProtocolMismatchWithVersionReturnsMismatchWithVersion()
    {
        // Arrange
        var read = PipeReadResult.FromError(new PipeError
                                            {
                                                Kind = PipeErrorKind.ProtocolMismatch,
                                                ReceivedVersion = 3,
                                                ExpectedVersion = 2
                                            });

        // Act
        var status = PipeStatusRule.ForRead(read);

        // Assert
        Assert.AreEqual(new PipeConnectionStatus
                        {
                            State = PipeConnectionState.ProtocolMismatch,
                            ReceivedVersion = 3
                        },
                        status,
                        "A mismatch should carry the received version");
    }

    /// <summary>
    /// A protocol mismatch of a frame before any version has no received version.
    /// </summary>
    [TestMethod]
    public void PipeStatusRuleForReadProtocolMismatchWithoutVersionReturnsMismatchWithoutVersion()
    {
        // Arrange
        var read = PipeReadResult.FromError(new PipeError
                                            {
                                                Kind = PipeErrorKind.ProtocolMismatch,
                                                ExpectedVersion = 2,
                                                TypeValue = 3
                                            });

        // Act
        var status = PipeStatusRule.ForRead(read);

        // Assert
        Assert.AreEqual(new PipeConnectionStatus
                        {
                            State = PipeConnectionState.ProtocolMismatch
                        },
                        status,
                        "A mismatch before any version should carry no version");
    }

    /// <summary>
    /// An error that ends the stream is the error state with its kind.
    /// </summary>
    /// <param name="kind">The kind of the error</param>
    [TestMethod]
    [DataRow(PipeErrorKind.TruncatedFrame)]
    [DataRow(PipeErrorKind.OversizedFrame)]
    [DataRow(PipeErrorKind.ReadFailed)]
    public void PipeStatusRuleForReadStreamEndingErrorReturnsErrorWithKind(PipeErrorKind kind)
    {
        // Arrange
        var read = PipeReadResult.FromError(new PipeError
                                            {
                                                Kind = kind
                                            });

        // Act
        var status = PipeStatusRule.ForRead(read);

        // Assert
        Assert.AreEqual(new PipeConnectionStatus
                        {
                            State = PipeConnectionState.Error,
                            ErrorKind = kind
                        },
                        status,
                        "A stream-ending error should be the error state with its kind");
    }

    /// <summary>
    /// The end of the stream means the game is gone.
    /// </summary>
    [TestMethod]
    public void PipeStatusRuleForReadEndReturnsWaitingForGame()
    {
        // Arrange
        var read = PipeReadResult.End;

        // Act
        var status = PipeStatusRule.ForRead(read);

        // Assert
        Assert.AreEqual(new PipeConnectionStatus
                        {
                            State = PipeConnectionState.WaitingForGame
                        },
                        status,
                        "The end of the stream should mean waiting for the game");
    }

    #endregion // Methods
}