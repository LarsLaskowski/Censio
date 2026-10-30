using System.Reflection;

using Censio.Contracts;

namespace Censio.Server.Data.Tests;

/// <summary>
/// Tests for <see cref="ServerDataAssembly" />.
/// </summary>
[TestClass]
public sealed class ServerDataAssemblyTests
{
    #region Methods

    /// <summary>
    /// The assembly reference carries the build version of the solution.
    /// </summary>
    [TestMethod]
    public void ServerDataAssemblyReferenceCarriesBuildVersion()
    {
        // Arrange
        var expected = ReadBuildVersion(typeof(ServerDataAssembly).Assembly);

        // Act
        var actual = ProductVersion.Read(ServerDataAssembly.Reference);

        // Assert
        Assert.AreEqual(expected, actual, "The assembly of ServerDataAssembly should carry the build version as its informational version");
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