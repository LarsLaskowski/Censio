using System.Reflection;

using Censio.Contracts;

namespace Censio.Pipe.Tests;

/// <summary>
/// Tests for <see cref="PipeAssembly" />.
/// </summary>
[TestClass]
public sealed class PipeAssemblyTests
{
    #region Methods

    /// <summary>
    /// The assembly reference carries the build version of the solution.
    /// </summary>
    [TestMethod]
    public void PipeAssemblyReferenceCarriesBuildVersion()
    {
        // Arrange
        var expected = ReadBuildVersion(typeof(PipeAssembly).Assembly);

        // Act
        var actual = ProductVersion.Read(PipeAssembly.Reference);

        // Assert
        Assert.AreEqual(expected, actual, "The assembly of PipeAssembly should carry the build version as its informational version");
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