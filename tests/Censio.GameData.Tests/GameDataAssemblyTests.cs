using System.Reflection;

using Censio.Contracts;

namespace Censio.GameData.Tests;

/// <summary>
/// Tests for <see cref="GameDataAssembly" />.
/// </summary>
[TestClass]
public sealed class GameDataAssemblyTests
{
    #region Methods

    /// <summary>
    /// The assembly reference carries the build version of the solution.
    /// </summary>
    [TestMethod]
    public void GameDataAssemblyReferenceCarriesBuildVersion()
    {
        // Arrange
        var expected = ReadBuildVersion(typeof(GameDataAssembly).Assembly);

        // Act
        var actual = ProductVersion.Read(GameDataAssembly.Reference);

        // Assert
        Assert.AreEqual(expected, actual, "The assembly of GameDataAssembly should carry the build version as its informational version");
    }

    /// <summary>
    /// Reads the build version stamped into an assembly from <c>Directory.Build.props</c>.
    /// </summary>
    /// <param name="assembly">The assembly to read</param>
    /// <returns>The value of the <c>CensioBuildVersion</c> metadata</returns>
    private static string ReadBuildVersion(Assembly assembly)
    {
        return assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                       .Single(attribute => attribute.Key == "CensioBuildVersion")
                       .Value!;
    }

    #endregion // Methods
}