using Bunit;

using Censio.Server.Components;

using TestContext = Microsoft.VisualStudio.TestTools.UnitTesting.TestContext;

namespace Censio.Server.Tests;

/// <summary>
/// Tests for <see cref="TrendTooltip" />.
/// </summary>
[TestClass]
public sealed class TrendTooltipTests
{
    #region Fields

    /// <summary>
    /// The lines of a tooltip, one of them with markup characters.
    /// </summary>
    private static readonly string[] _lines = ["2026-11-13 12:02", "<b>Production</b>: 1.00"];

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="TrendTooltipTests" /> class.
    /// </summary>
    /// <param name="testContext">The test context</param>
    public TrendTooltipTests(TestContext testContext)
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
    /// A tooltip without lines draws nothing.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC9")]
    public async Task TrendTooltipWithoutLinesDrawsNothing()
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        // Act
        var tooltip = host.Render<TrendTooltip>(UiLanguage.English, parameters => parameters.Add(component => component.Lines, []));

        // Assert
        Assert.AreEqual(string.Empty, tooltip.Markup.Trim(), "A tooltip without lines should draw nothing");
    }

    /// <summary>
    /// A tooltip with lines draws a box that holds each line as text, markup characters included.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC11")]
    public async Task TrendTooltipWithLinesDrawsBoxWithTextLines()
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        // Act
        var tooltip = host.Render<TrendTooltip>(UiLanguage.English, parameters => parameters.Add(component => component.Lines, _lines).Add(component => component.Color, "#112233"));

        // Assert
        Assert.HasCount(1, tooltip.FindAll("g.censio-chart-tooltip rect"), "The tooltip should draw one box");
        Assert.AreSequenceEqual(_lines, tooltip.FindAll("text").Select(line => line.TextContent).ToList(), "Each line should be drawn as text");
        Assert.IsEmpty(tooltip.FindAll("b"), "A line should not be read as markup");
    }

    #endregion // Methods
}