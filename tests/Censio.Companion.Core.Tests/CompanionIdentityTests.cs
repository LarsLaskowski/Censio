using Censio.GameData;

namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for <see cref="CompanionIdentity" />.
/// </summary>
[TestClass]
public sealed class CompanionIdentityTests
{
    #region Methods

    /// <summary>
    /// The identity holds the module version ids of the assemblies that extract and write the cache, in the <c>D</c> format,
    /// joined by a semicolon, and is the same at every request.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void CompanionIdentityCurrentHoldsBothModuleIds()
    {
        // Arrange
        var expected = new[]
                       {
                           typeof(GameCatalog).Module.ModuleVersionId.ToString("D"),
                           typeof(CompanionIdentity).Module.ModuleVersionId.ToString("D")
                       };

        // Act
        var identity = CompanionIdentity.Current;

        // Assert
        var parts = identity.Split(';');

        Assert.HasCount(2, parts, "The identity should hold two parts");
        Assert.AreSequenceEqual(expected.Order(StringComparer.Ordinal).ToList(), parts.Order(StringComparer.Ordinal).ToList(), "The parts should be the module version ids of the game data and the Companion core");
        Assert.AreEqual(identity, CompanionIdentity.Current, "The identity should be the same at every request");
    }

    #endregion // Methods
}