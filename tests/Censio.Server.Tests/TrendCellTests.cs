using AngleSharp.Dom;

using Bunit;

using Censio.Server.Components;

using TestContext = Microsoft.VisualStudio.TestTools.UnitTesting.TestContext;

namespace Censio.Server.Tests;

/// <summary>
/// Tests for the trend cell of a table, <see cref="TrendCell" />.
/// </summary>
[TestClass]
public sealed class TrendCellTests
{
    #region Constants

    /// <summary>
    /// The selector of the arrow of a cell.
    /// </summary>
    private const string ArrowSelector = "span[role=img]";

    /// <summary>
    /// The path data of the lines of the tests that are not built.
    /// </summary>
    private const string PlainPath = "M0 18L80 2";

    #endregion // Constants

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="TrendCellTests" /> class.
    /// </summary>
    /// <param name="testContext">The test context</param>
    public TrendCellTests(TestContext testContext)
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
    /// The arrow of a line is the sign of its direction, carries the direction's name as its accessible name and tooltip in
    /// the language shown, and takes the text colour.
    /// </summary>
    /// <param name="direction">The direction</param>
    /// <param name="language">The language shown</param>
    /// <param name="arrow">The sign of the arrow</param>
    /// <param name="name">The name of the direction</param>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC3")]
    [DataRow(TrendDirection.Rising, UiLanguage.English, "↑", "Rising")]
    [DataRow(TrendDirection.Falling, UiLanguage.English, "↓", "Falling")]
    [DataRow(TrendDirection.Stable, UiLanguage.English, "→", "Stable")]
    [DataRow(TrendDirection.Rising, UiLanguage.German, "↑", "Steigend")]
    [DataRow(TrendDirection.Falling, UiLanguage.German, "↓", "Fallend")]
    [DataRow(TrendDirection.Stable, UiLanguage.German, "→", "Gleichbleibend")]
    public async Task TrendCellArrowCarriesItsNameInBothLanguages(TrendDirection direction, UiLanguage language, string arrow, string name)
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        var line = new SparklineView
                   {
                       Path = PlainPath,
                       Direction = direction
                   };

        // Act
        var cell = RenderCell(host, language, line, true);

        // Assert
        var arrows = cell.FindAll(ArrowSelector);

        Assert.HasCount(1, arrows, "A line should have one arrow");
        Assert.AreEqual(arrow, arrows[0].TextContent, $"The arrow of {direction} should be its sign");
        Assert.AreEqual(name, arrows[0].GetAttribute("aria-label"), $"The arrow should carry the name of {direction} in {language} as its accessible name");
        Assert.AreEqual(name, arrows[0].GetAttribute("title"), $"The arrow should carry the name of {direction} in {language} as its tooltip");
        Assert.IsNull(arrows[0].GetAttribute("style"), "The arrow should take the text colour");
    }

    /// <summary>
    /// The sparkline is hidden from the accessibility tree, drawn in the sparkline colour of the theme shown, and carries
    /// nothing the reveal animation of the charts applies to.
    /// </summary>
    /// <param name="theme">The theme shown</param>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC3")]
    [DataRow(UiTheme.Light)]
    [DataRow(UiTheme.Dark)]
    public async Task TrendCellSparklineHiddenFromAccessibilityAndNotAnimated(UiTheme theme)
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        host.Theme = theme;

        var line = new SparklineView
                   {
                       Path = PlainPath,
                       Direction = TrendDirection.Rising
                   };

        // Act
        var cell = RenderCell(host, UiLanguage.English, line, true);

        // Assert
        var svg = cell.Find("svg");
        var path = cell.Find("svg path");

        Assert.AreEqual("true", svg.GetAttribute("aria-hidden"), "The sparkline should be hidden from the accessibility tree");
        Assert.AreEqual("false", svg.GetAttribute("focusable"), "The sparkline should not take the focus");
        Assert.IsTrue(svg.ClassList.Contains("censio-sparkline"), "The sparkline should carry its own class");
        Assert.AreEqual(PlainPath, path.GetAttribute("d"), "The path should draw the line's path data");
        Assert.AreEqual(TrendPalette.Sparkline(theme), path.GetAttribute("stroke"), $"The line should be drawn in the sparkline colour of the {theme} theme");
        Assert.AreEqual("none", path.GetAttribute("fill"), "The line should not be filled");
        Assert.IsEmpty(cell.FindAll(".mud-chart-line, .censio-chart, animate, animateTransform, animateMotion, set"), "Nothing of the cell should be animated or carry a class the charts' reveal applies to");
    }

    /// <summary>
    /// A read cell without a line shows a dash and neither a sparkline nor an arrow.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC4")]
    public async Task TrendCellWithoutLineShowsDash()
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        // Act
        var cell = RenderCell(host, UiLanguage.English, null, true);

        // Assert
        Assert.AreEqual("–", PageTestHost.Text(cell).Trim(), "A cell without a line should show a dash");
        Assert.IsEmpty(cell.FindAll("svg"), "A cell without a line should have no sparkline");
        Assert.IsEmpty(cell.FindAll(ArrowSelector), "A cell without a line should have no arrow");
    }

    /// <summary>
    /// The cell of a line of 240 points whose x coordinates all need a decimal holds one SVG with one path of coordinates
    /// only, with no title, hover point, axis, label or legend, and its markup takes at most 4,000 characters.
    /// </summary>
    /// <returns>A task that represents the asynchronous test</returns>
    [TestMethod]
    [TestCategory("AC8")]
    public async Task TrendCellMarkupHoldsOneSvgPathOfCoordinatesOnly()
    {
        // Arrange
        await using var host = await PageTestHost.CreateAsync(TestContext.CancellationToken);

        var line = SparklineBuilder.Build(SparklineBuilderTests.MaxPointSamples(), SparklineBuilderTests.Range(), 2);

        // Act
        var cell = RenderCell(host, UiLanguage.English, line, true);

        // Assert
        var svgs = cell.FindAll("svg");

        Assert.HasCount(1, svgs, "The cell should hold one sparkline");
        Assert.AreSequenceEqual(["path"], svgs[0].Descendants<IElement>().Select(element => element.LocalName), "The sparkline should hold one path and nothing else");
        Assert.AreEqual(line?.Path, svgs[0].Descendants<IElement>().First().GetAttribute("d"), "The path should draw the built line");
        Assert.IsLessThanOrEqualTo(SparklineBuilderTests.MarkupLimit, cell.Markup.Length, "The markup of a cell of 240 points should take at most 4,000 characters");
    }

    /// <summary>
    /// Renders a trend cell.
    /// </summary>
    /// <param name="host">The host</param>
    /// <param name="language">The language shown</param>
    /// <param name="line">The line, or <see langword="null" /></param>
    /// <param name="isRead">Whether the sparklines were read</param>
    /// <returns>The rendered cell</returns>
    private static IRenderedComponent<TrendCell> RenderCell(PageTestHost host, UiLanguage language, SparklineView? line, bool isRead)
    {
        return host.Render<TrendCell>(language,
                                      parameters => parameters.Add(component => component.Line, line)
                                                              .Add(component => component.IsRead, isRead));
    }

    #endregion // Methods
}