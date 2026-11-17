using System.Text.Json;

using Microsoft.JSInterop;

using TestContext = Microsoft.VisualStudio.TestTools.UnitTesting.TestContext;

namespace Censio.Server.Tests;

/// <summary>
/// Tests for <see cref="BrowserOffset" />.
/// </summary>
[TestClass]
public sealed class BrowserOffsetTests
{
    #region Fields

    /// <summary>
    /// The instant the browser is asked about.
    /// </summary>
    private static readonly DateTimeOffset _instant = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="BrowserOffsetTests" /> class.
    /// </summary>
    /// <param name="testContext">The test context</param>
    public BrowserOffsetTests(TestContext testContext)
    {
        TestContext = testContext;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the test context.
    /// </summary>
    public TestContext TestContext { get; }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// An answer within -840 to 840 minutes is the offset, the bounds included; an answer outside is none.
    /// </summary>
    /// <param name="answer">The answer of the browser</param>
    /// <param name="expected">The offset, or <see langword="null" /> for none</param>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC9")]
    [DataRow(0, 0)]
    [DataRow(60, 60)]
    [DataRow(-840, -840)]
    [DataRow(840, 840)]
    [DataRow(841, null)]
    [DataRow(-841, null)]
    public async Task BrowserOffsetAskAsyncAnswersOnlyWithinBounds(int answer, int? expected)
    {
        // Arrange
        var runtime = new FakeJsRuntime
                      {
                          Result = answer
                      };

        // Act
        var offset = await BrowserOffset.AskAsync(runtime, _instant, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(expected, offset, $"The answer {answer} should give {expected?.ToString() ?? "none"}");
    }

    /// <summary>
    /// A failed call of the browser, a disconnected circuit or a cancelled call is no answer.
    /// </summary>
    /// <param name="failure">The kind of the failure</param>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC9")]
    [DataRow(nameof(JSException))]
    [DataRow(nameof(JsonException))]
    [DataRow(nameof(JSDisconnectedException))]
    [DataRow(nameof(OperationCanceledException))]
    public async Task BrowserOffsetAskAsyncFailureGivesNone(string failure)
    {
        // Arrange
        var runtime = new FakeJsRuntime
                      {
                          Failure = CreateFailure(failure)
                      };

        // Act
        var offset = await BrowserOffset.AskAsync(runtime, _instant, TestContext.CancellationToken);

        // Assert
        Assert.IsNull(offset, $"A {failure} should give no offset");
        Assert.HasCount(1, runtime.Calls, "The browser should have been asked once");
    }

    /// <summary>
    /// A failure of another kind is a defect and is not swallowed.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC9")]
    public async Task BrowserOffsetAskAsyncOtherFailurePropagates()
    {
        // Arrange
        var runtime = new FakeJsRuntime
                      {
                          Failure = new InvalidOperationException("Not a browser failure")
                      };

        // Act
        var thrown = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => BrowserOffset.AskAsync(runtime, _instant, TestContext.CancellationToken));

        // Assert
        Assert.AreEqual("Not a browser failure", thrown.Message, "The failure should reach the caller unchanged");
    }

    /// <summary>
    /// The browser is asked for the offset at the instant, written in UTC with milliseconds.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC9")]
    public async Task BrowserOffsetAskAsyncSendsInstantInUtc()
    {
        // Arrange
        var runtime = new FakeJsRuntime
                      {
                          Result = 120
                      };

        var instant = new DateTimeOffset(2026, 11, 13, 14, 30, 5, 7, TimeSpan.FromHours(2));

        // Act
        await BrowserOffset.AskAsync(runtime, instant, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual("censio.utcOffsetMinutes", runtime.Calls[0].Identifier, "The browser function should be the offset function");
        Assert.AreSequenceEqual(new object?[] { "2026-11-13T12:30:05.007Z" }, runtime.Calls[0].Arguments, "The instant should be sent in UTC");
    }

    /// <summary>
    /// Creates the exception of a failure.
    /// </summary>
    /// <param name="failure">The kind of the failure</param>
    /// <returns>The exception</returns>
    private static Exception CreateFailure(string failure)
    {
        return failure switch
               {
                   nameof(JSException) => new JSException("The call failed"),
                   nameof(JsonException) => new JsonException("The answer is no number"),
                   nameof(JSDisconnectedException) => new JSDisconnectedException("The circuit is gone"),
                   _ => new OperationCanceledException()
               };
    }

    #endregion // Methods
}