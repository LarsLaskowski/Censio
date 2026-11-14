using System.Globalization;
using System.Text.Json;

using Bunit;
using Bunit.JSInterop;

using Censio.Contracts;
using Censio.Server.Components;

using Microsoft.JSInterop;

using TestContext = Microsoft.VisualStudio.TestTools.UnitTesting.TestContext;

namespace Censio.Server.Tests;

/// <summary>
/// Tests for the Companion banner and the as-of line, <see cref="DataStatus" />.
/// </summary>
[TestClass]
public sealed class DataStatusTests
{
    #region Constants

    /// <summary>
    /// The browser function that gives the UTC offset of an instant.
    /// </summary>
    private const string UtcOffsetMinutes = "censio.utcOffsetMinutes";

    /// <summary>
    /// The banner while no Companion is connected.
    /// </summary>
    private const string NotConnected = "The Companion is not connected.";

    /// <summary>
    /// The banner while the game is not running.
    /// </summary>
    private const string GameNotRunning = "The game is not running.";

    /// <summary>
    /// The banner while the Companion cannot read the game's statistics.
    /// </summary>
    private const string Unreadable = "The Companion cannot read the game's statistics.";

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The receive time of the shown data.
    /// </summary>
    private static readonly DateTimeOffset _asOf = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="DataStatusTests" /> class.
    /// </summary>
    /// <param name="testContext">The test context</param>
    public DataStatusTests(TestContext testContext)
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
    /// The as-of line shows the receive time at the UTC offset the browser gives for that instant.
    /// </summary>
    /// <param name="language">The language shown</param>
    /// <param name="offsetMinutes">The offset the browser gives</param>
    /// <param name="expected">The as-of line</param>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC8")]
    [TestCategory("AC3")]
    [DataRow(UiLanguage.English, 60, "As of 2026-11-13 13:00")]
    [DataRow(UiLanguage.German, -300, "Stand: 2026-11-13 07:00")]
    [DataRow(UiLanguage.English, 840, "As of 2026-11-14 02:00")]
    [DataRow(UiLanguage.English, -840, "As of 2026-11-12 22:00")]
    public async Task DataStatusShowsAsOfAtBrowserOffset(UiLanguage language, int offsetMinutes, string expected)
    {
        // Arrange
        await using var host = await CreateConnectedHostAsync();

        host.Context.JSInterop.Setup<int>(UtcOffsetMinutes, _ => true).SetResult(offsetMinutes);

        // Act
        var status = Render(host, language);

        // Assert
        await status.WaitForAssertionAsync(() => Assert.AreEqual(expected, PageTestHost.Text(status).Trim(), "The as-of line should show the time at the browser's offset"), PageTestHost.WaitBound);
    }

    /// <summary>
    /// While the browser has not answered about its offset, the as-of line is an empty paragraph without time and suffix.
    /// </summary>
    /// <param name="language">The language shown</param>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC3")]
    [DataRow(UiLanguage.English)]
    [DataRow(UiLanguage.German)]
    public async Task DataStatusShowsNoTimeWhileBrowserHasNotAnswered(UiLanguage language)
    {
        // Arrange
        await using var host = await CreateConnectedHostAsync();

        host.Context.JSInterop.Setup<int>(UtcOffsetMinutes, _ => true);

        // Act
        var status = Render(host, language);

        // Assert
        Assert.HasCount(1, status.FindAll("p"), "The as-of line should be one paragraph");
        Assert.AreEqual(string.Empty, PageTestHost.Text(status).Trim(), "The as-of line should be empty until the browser answers");
        Assert.DoesNotContain("UTC", status.Markup, "The markup should not name UTC until the browser answers");
    }

    /// <summary>
    /// A new as-of time shows at once at the last offset the browser gave, never as an empty line, while the browser's
    /// answer for the new instant is outstanding.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task DataStatusNewAsOfShowsAtLastOffsetWhileAsking()
    {
        // Arrange
        await using var host = await CreateConnectedHostAsync();
        var nextAsOf = _asOf.AddHours(1);

        host.Context.JSInterop.Setup<int>(UtcOffsetMinutes, arguments => IsInstant(arguments, _asOf)).SetResult(60);
        host.Context.JSInterop.Setup<int>(UtcOffsetMinutes, arguments => IsInstant(arguments, nextAsOf));

        var status = Render(host, UiLanguage.English);

        await status.WaitForAssertionAsync(() => Assert.AreEqual("As of 2026-11-13 13:00", PageTestHost.Text(status).Trim(), "The first answer should set the offset"), PageTestHost.WaitBound);

        // Act
        status.Render(parameters => parameters.Add(component => component.AsOf, nextAsOf));

        // Assert
        await status.WaitForAssertionAsync(() => Assert.HasCount(2, host.Context.JSInterop.Invocations[UtcOffsetMinutes], "The browser should be asked for the new instant"), PageTestHost.WaitBound);
        Assert.AreEqual("As of 2026-11-13 14:00", PageTestHost.Text(status).Trim(), "The new time should show at the last offset while the second answer is outstanding");
    }

    /// <summary>
    /// An offset outside -840 to 840 minutes counts as none: the as-of line shows UTC.
    /// </summary>
    /// <param name="offsetMinutes">The offset the browser gives</param>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC8")]
    [TestCategory("AC4")]
    [DataRow(841)]
    [DataRow(-841)]
    [DataRow(int.MaxValue)]
    [DataRow(int.MinValue)]
    public async Task DataStatusOutOfRangeOffsetShowsUtc(int offsetMinutes)
    {
        // Arrange
        await using var host = await CreateConnectedHostAsync();

        host.Context.JSInterop.Setup<int>(UtcOffsetMinutes, _ => true).SetResult(offsetMinutes);

        // Act
        var status = Render(host, UiLanguage.English);

        // Assert
        await status.WaitForAssertionAsync(() => Assert.HasCount(1, host.Context.JSInterop.Invocations[UtcOffsetMinutes], "The browser should be asked for its offset once"), PageTestHost.WaitBound);
        await status.WaitForAssertionAsync(() => Assert.AreEqual("As of 2026-11-13 12:00 UTC", PageTestHost.Text(status).Trim(), "An offset out of range should leave the time in UTC"), PageTestHost.WaitBound);
    }

    /// <summary>
    /// A failed browser call or an answer of the wrong type keeps the time in UTC.
    /// </summary>
    /// <param name="failure">The kind of the failure</param>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC8")]
    [TestCategory("AC4")]
    [DataRow(nameof(JSException))]
    [DataRow(nameof(JsonException))]
    public async Task DataStatusInteropFailureShowsUtc(string failure)
    {
        // Arrange
        await using var host = await CreateConnectedHostAsync();

        host.Context.JSInterop.Setup<int>(UtcOffsetMinutes, _ => true).SetException(CreateFailure(failure));

        // Act
        var status = Render(host, UiLanguage.English);

        // Assert
        await status.WaitForAssertionAsync(() => Assert.HasCount(1, host.Context.JSInterop.Invocations[UtcOffsetMinutes], "The browser should be asked for its offset once"), PageTestHost.WaitBound);
        await status.WaitForAssertionAsync(() => Assert.AreEqual("As of 2026-11-13 12:00 UTC", PageTestHost.Text(status).Trim(), "A failed browser call should leave the time in UTC"), PageTestHost.WaitBound);
    }

    /// <summary>
    /// The banner follows the Companion's connection and its last reported pipe state.
    /// </summary>
    /// <param name="isConnected">Whether a Companion is connected</param>
    /// <param name="pipeState">The last reported pipe state, or <see langword="null" /> for none since the hello</param>
    /// <param name="expected">The banner texts, joined by <c>|</c></param>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC8")]
    [DataRow(false, null, NotConnected)]
    [DataRow(true, null, "")]
    [DataRow(true, ContractPipeState.WaitingForGame, GameNotRunning)]
    [DataRow(true, ContractPipeState.PipeBusy, Unreadable)]
    [DataRow(true, ContractPipeState.ProtocolMismatch, Unreadable)]
    [DataRow(true, ContractPipeState.Error, Unreadable)]
    [DataRow(true, ContractPipeState.Connected, "")]
    [DataRow(true, ContractPipeState.Receiving, "")]
    public async Task DataStatusBannerPerCompanionState(bool isConnected, ContractPipeState? pipeState, string expected)
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        SetCompanion(host.State, isConnected, pipeState);

        // Act
        var status = Render(host, UiLanguage.English);

        // Assert
        Assert.AreEqual(expected, Banners(status), "The banner should follow the Companion's state");
    }

    /// <summary>
    /// The banner texts are those of the spec in German as well.
    /// </summary>
    /// <param name="isConnected">Whether a Companion is connected</param>
    /// <param name="pipeState">The last reported pipe state</param>
    /// <param name="expected">The banner text</param>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC8")]
    [DataRow(false, null, "Der Companion ist nicht verbunden.")]
    [DataRow(true, ContractPipeState.WaitingForGame, "Das Spiel läuft nicht.")]
    [DataRow(true, ContractPipeState.Error, "Der Companion kann die Statistik des Spiels nicht lesen.")]
    public async Task DataStatusBannerShowsGermanTexts(bool isConnected, ContractPipeState? pipeState, string expected)
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        SetCompanion(host.State, isConnected, pipeState);

        // Act
        var status = Render(host, UiLanguage.German);

        // Assert
        Assert.AreEqual(expected, Banners(status), "The banner should be in German");
    }

    /// <summary>
    /// A Companion's connect, a status with another pipe state and its disconnect change the banner without a reload.
    /// </summary>
    /// <param name="isConnected">Whether a Companion is connected before the change</param>
    /// <param name="pipeState">The pipe state before the change</param>
    /// <param name="change">The change: <c>connect</c>, <c>status</c> or <c>disconnect</c></param>
    /// <param name="expected">The banner texts after the change, joined by <c>|</c></param>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC8")]
    [DataRow(false, null, "connect", "")]
    [DataRow(true, ContractPipeState.Receiving, "status", Unreadable)]
    [DataRow(true, ContractPipeState.WaitingForGame, "disconnect", NotConnected)]
    public async Task DataStatusBannerChangesLive(bool isConnected, ContractPipeState? pipeState, string change, string expected)
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        SetCompanion(host.State, isConnected, pipeState);

        var status = Render(host, UiLanguage.English);

        // Act
        Change(host.State, change);

        // Assert
        await status.WaitForAssertionAsync(() => Assert.AreEqual(expected, Banners(status), "The banner should change without a reload"), PageTestHost.WaitBound);
    }

    /// <summary>
    /// A profile notice does not render the banner again; only a Companion notice does.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC8")]
    public async Task DataStatusProfileNoticeDoesNotRender()
    {
        // Arrange
        await using var host = await CreateConnectedHostAsync();

        var status = host.Render<DataStatus>(UiLanguage.English,
                                             _ =>
                                             {
                                             });
        var rendersBefore = status.RenderCount;

        // Act
        await host.StoreProfileAsync(Guid.NewGuid(), "First");

        // Assert
        host.State.SetCompanionPipeState(ContractPipeState.WaitingForGame);

        await status.WaitForAssertionAsync(() => Assert.AreEqual(GameNotRunning, Banners(status), "The Companion notice should change the banner"), PageTestHost.WaitBound);
        Assert.AreEqual(rendersBefore + 1, status.RenderCount, "Only the Companion notice should render the banner again");
    }

    /// <summary>
    /// Renders the component with the as-of time.
    /// </summary>
    /// <param name="host">The host</param>
    /// <param name="language">The language shown</param>
    /// <returns>The rendered component</returns>
    private static IRenderedComponent<DataStatus> Render(PageTestHost host, UiLanguage language)
    {
        return host.Render<DataStatus>(language, parameters => parameters.Add(status => status.AsOf, _asOf));
    }

    /// <summary>
    /// Tells whether the browser call asks for the offset at an instant.
    /// </summary>
    /// <param name="arguments">The call's arguments</param>
    /// <param name="instant">The instant</param>
    /// <returns><see langword="true" /> when the call's first argument is the instant</returns>
    private static bool IsInstant(JSRuntimeInvocation arguments, DateTimeOffset instant)
    {
        return arguments.Arguments.Count > 0
               && Equals(arguments.Arguments[0], instant.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Gives the texts of the shown banners, joined by <c>|</c>.
    /// </summary>
    /// <param name="status">The rendered component</param>
    /// <returns>The texts</returns>
    private static string Banners(IRenderedComponent<DataStatus> status)
    {
        return string.Join('|', status.FindAll(".mud-alert").Select(alert => alert.TextContent.Trim()));
    }

    /// <summary>
    /// Sets the Companion's state.
    /// </summary>
    /// <param name="state">The server state</param>
    /// <param name="isConnected">Whether a Companion is connected</param>
    /// <param name="pipeState">The last reported pipe state, or <see langword="null" /> for none</param>
    private static void SetCompanion(ServerState state, bool isConnected, ContractPipeState? pipeState)
    {
        if (isConnected)
        {
            state.SetCompanionConnected();
        }

        if (pipeState is { } reported)
        {
            state.SetCompanionPipeState(reported);
        }
    }

    /// <summary>
    /// Changes the Companion's state.
    /// </summary>
    /// <param name="state">The server state</param>
    /// <param name="change">The change: <c>connect</c>, <c>status</c> or <c>disconnect</c></param>
    private static void Change(ServerState state, string change)
    {
        switch (change)
        {
            case "connect":
                state.SetCompanionConnected();
                break;

            case "status":
                state.SetCompanionPipeState(ContractPipeState.PipeBusy);
                break;

            default:
                state.SetCompanionDisconnected();
                break;
        }
    }

    /// <summary>
    /// Creates the failure of a browser call.
    /// </summary>
    /// <param name="failure">The kind of the failure</param>
    /// <returns>The exception</returns>
    private static Exception CreateFailure(string failure)
    {
        return failure == nameof(JSException)
                   ? new JSException("The browser failed")
                   : new JsonException("The answer is not a number");
    }

    /// <summary>
    /// Creates a host with a connected Companion that receives, so that no banner is shown.
    /// </summary>
    /// <returns>The host</returns>
    private async Task<PageTestHost> CreateConnectedHostAsync()
    {
        var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        SetCompanion(host.State, true, ContractPipeState.Receiving);

        return host;
    }

    #endregion // Methods
}