namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for <see cref="PipeConnectResult" />.
/// </summary>
[TestClass]
public sealed class PipeConnectResultTests
{
    #region Methods

    /// <summary>
    /// A timeout while the pipe is listed means another reader holds it.
    /// </summary>
    [TestMethod]
    public void PipeConnectResultFromFailureTimeoutWithPipeIsBusy()
    {
        // Arrange
        var exception = new TimeoutException();

        // Act
        var result = PipeConnectResult.FromFailure(exception, true);

        // Assert
        Assert.AreEqual(PipeConnectResultKind.Busy, result.Kind, "A timeout with a listed pipe should be busy");
        Assert.IsNull(result.Stream, "A busy result should carry no stream");
        Assert.IsNull(result.ErrorCode, "A busy result should carry no error code");
    }

    /// <summary>
    /// A timeout while the pipe is not listed means the pipe does not exist.
    /// </summary>
    [TestMethod]
    public void PipeConnectResultFromFailureTimeoutWithoutPipeIsNotFound()
    {
        // Arrange
        var exception = new TimeoutException();

        // Act
        var result = PipeConnectResult.FromFailure(exception, false);

        // Assert
        Assert.AreEqual(PipeConnectResultKind.NotFound, result.Kind, "A timeout without a listed pipe should be not found");
        Assert.IsNull(result.ErrorCode, "A not-found result should carry no error code");
    }

    /// <summary>
    /// A file-not-found exception means the pipe does not exist, whatever the listing says.
    /// </summary>
    /// <param name="pipeExists">Whether the pipe is listed</param>
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void PipeConnectResultFromFailureFileNotFoundIsNotFound(bool pipeExists)
    {
        // Arrange
        var exception = new FileNotFoundException();

        // Act
        var result = PipeConnectResult.FromFailure(exception, pipeExists);

        // Assert
        Assert.AreEqual(PipeConnectResultKind.NotFound, result.Kind, "A file-not-found exception should be not found");
    }

    /// <summary>
    /// Access denied fails with Windows error code 5.
    /// </summary>
    [TestMethod]
    public void PipeConnectResultFromFailureUnauthorizedAccessIsFailedFive()
    {
        // Arrange
        var exception = new UnauthorizedAccessException();

        // Act
        var result = PipeConnectResult.FromFailure(exception, true);

        // Assert
        Assert.AreEqual(PipeConnectResultKind.Failed, result.Kind, "Access denied should be a failed attempt");
        Assert.AreEqual(5, result.ErrorCode, "Access denied should carry Windows error code 5");
    }

    /// <summary>
    /// An I/O exception with a Win32 HRESULT carries the Win32 code.
    /// </summary>
    [TestMethod]
    public void PipeConnectResultFromFailureIOExceptionCarriesWin32Code()
    {
        // Arrange
        var exception = new IOException("sharing violation", unchecked((int)0x80070020));

        // Act
        var result = PipeConnectResult.FromFailure(exception, true);

        // Assert
        Assert.AreEqual(PipeConnectResultKind.Failed, result.Kind, "An I/O failure should be a failed attempt");
        Assert.AreEqual(0x20, result.ErrorCode, "The error code should be the low 16 bits of the HRESULT");
    }

    /// <summary>
    /// Any other exception fails without an error code.
    /// </summary>
    /// <param name="hresult">The HRESULT of the exception</param>
    [TestMethod]
    [DataRow(unchecked((int)0x80131620))]
    [DataRow(unchecked((int)0x80040005))]
    public void PipeConnectResultFromFailureOtherExceptionHasNoCode(int hresult)
    {
        // Arrange
        var exception = new IOException("other failure", hresult);

        // Act
        var result = PipeConnectResult.FromFailure(exception, true);

        // Assert
        Assert.AreEqual(PipeConnectResultKind.Failed, result.Kind, "Another failure should be a failed attempt");
        Assert.IsNull(result.ErrorCode, "A failure without a Win32 HRESULT should carry no error code");
    }

    /// <summary>
    /// An exception that is not an I/O exception fails without an error code.
    /// </summary>
    [TestMethod]
    public void PipeConnectResultFromFailureInvalidOperationHasNoCode()
    {
        // Arrange
        var exception = new InvalidOperationException();

        // Act
        var result = PipeConnectResult.FromFailure(exception, false);

        // Assert
        Assert.AreEqual(PipeConnectResultKind.Failed, result.Kind, "An unexpected exception should be a failed attempt");
        Assert.IsNull(result.ErrorCode, "An unexpected exception should carry no error code");
    }

    #endregion // Methods
}