using System.Reflection;

using Censio.Contracts;

namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for <see cref="TrayMenu" />.
/// </summary>
[TestClass]
public sealed class TrayMenuTests
{
    #region Methods

    /// <summary>
    /// The menu consists of the version entry, a separator and the exit entry.
    /// </summary>
    [TestMethod]
    public void TrayMenuCreateReturnsVersionSeparatorExit()
    {
        // Arrange
        var expectedKinds = new[] { TrayMenuEntryKind.Version, TrayMenuEntryKind.Separator, TrayMenuEntryKind.Exit };
        var expectedTexts = new[] { "Version 1.2.3", string.Empty, "Exit" };

        // Act
        var entries = TrayMenu.Create("1.2.3");

        // Assert
        Assert.HasCount(3, entries, "The menu should have exactly three entries");
        Assert.AreSequenceEqual(expectedKinds, entries.Select(entry => entry.Kind).ToArray(), "The entries should be version, separator, exit in this order");
        Assert.AreSequenceEqual(expectedTexts, entries.Select(entry => entry.Text).ToArray(), "The entry texts should be the version text, empty, Exit");
    }

    /// <summary>
    /// The version entry cannot be chosen.
    /// </summary>
    [TestMethod]
    public void TrayMenuCreateVersionEntryIsDisabled()
    {
        // Arrange
        const string version = "1.2.3";

        // Act
        var entries = TrayMenu.Create(version);

        // Assert
        Assert.IsFalse(entries.Single(entry => entry.Kind == TrayMenuEntryKind.Version).IsEnabled, "The version entry should be greyed out");
    }

    /// <summary>
    /// The exit entry can be chosen.
    /// </summary>
    [TestMethod]
    public void TrayMenuCreateExitEntryIsEnabled()
    {
        // Arrange
        const string version = "1.2.3";

        // Act
        var entries = TrayMenu.Create(version);

        // Assert
        Assert.IsTrue(entries.Single(entry => entry.Kind == TrayMenuEntryKind.Exit).IsEnabled, "The exit entry should be enabled");
    }

    /// <summary>
    /// The version entry shows the version of the Core assembly.
    /// </summary>
    [TestMethod]
    public void TrayMenuCreateVersionEntryShowsVersion()
    {
        // Arrange
        var version = ProductVersion.Read(typeof(TrayMenu).Assembly);

        // Act
        var entries = TrayMenu.Create(version);

        // Assert
        Assert.AreEqual("Version " + version, entries.Single(entry => entry.Kind == TrayMenuEntryKind.Version).Text, "The version entry should read Version followed by the product version");
    }

    /// <summary>
    /// The tooltip names the application.
    /// </summary>
    [TestMethod]
    public void TrayMenuToolTipIsCensioCompanion()
    {
        // Arrange
        const string expected = "Censio Companion";

        // Act
        var toolTip = TrayMenu.ToolTip;

        // Assert
        Assert.AreEqual(expected, toolTip, "The tooltip of the tray icon should be Censio Companion");
    }

    /// <summary>
    /// The tooltip shows the application name and the text of the state.
    /// </summary>
    /// <param name="state">The connection state</param>
    /// <param name="expected">The expected tooltip</param>
    [TestMethod]
    [DataRow(PipeConnectionState.WaitingForGame, "Censio Companion: Waiting for game")]
    [DataRow(PipeConnectionState.Connected, "Censio Companion: Connected")]
    [DataRow(PipeConnectionState.Receiving, "Censio Companion: Receiving")]
    [DataRow(PipeConnectionState.PipeBusy, "Censio Companion: Pipe busy")]
    [DataRow(PipeConnectionState.ProtocolMismatch, "Censio Companion: Protocol mismatch")]
    [DataRow(PipeConnectionState.Error, "Censio Companion: Error")]
    public void TrayMenuFormatToolTipReturnsTextPerState(PipeConnectionState state, string expected)
    {
        // Arrange
        // (state and expected come from the data row)

        // Act
        var toolTip = TrayMenu.FormatToolTip(state);

        // Assert
        Assert.AreEqual(expected, toolTip, "The tooltip should be the application name, a colon and the state text");
    }

    /// <summary>
    /// An undefined state is rejected.
    /// </summary>
    [TestMethod]
    public void TrayMenuFormatToolTipUndefinedStateThrows()
    {
        // Arrange
        var state = (PipeConnectionState)99;
        var call = () => TrayMenu.FormatToolTip(state);

        // Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(call, "An undefined state should be rejected");

        // Assert
        Assert.AreEqual("state", exception.ParamName, "The exception should name the parameter");
    }

    /// <summary>
    /// The Core assembly carries the build version of the solution.
    /// </summary>
    [TestMethod]
    public void TrayMenuCoreAssemblyCarriesBuildVersion()
    {
        // Arrange
        var assembly = typeof(TrayMenu).Assembly;
        var expected = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                               .Single(attribute => attribute.Key == "CensioBuildVersion")
                               .Value;

        // Act
        var actual = ProductVersion.Read(assembly);

        // Assert
        Assert.AreEqual(expected, actual, "The Core assembly should carry the build version as its informational version");
    }

    #endregion // Methods
}