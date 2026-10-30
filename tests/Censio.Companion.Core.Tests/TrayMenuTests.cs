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

    #endregion // Methods
}