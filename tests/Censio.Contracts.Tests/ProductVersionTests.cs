using System.Reflection;
using System.Reflection.Emit;

namespace Censio.Contracts.Tests;

/// <summary>
/// Tests for <see cref="ProductVersion" />.
/// </summary>
[TestClass]
public sealed class ProductVersionTests
{
    #region Methods

    /// <summary>
    /// The informational version attribute of an assembly is returned unchanged.
    /// </summary>
    [TestMethod]
    public void ProductVersionReadReturnsInformationalVersion()
    {
        // Arrange
        var assembly = CreateAssembly("InformationalVersion", new CustomAttributeBuilder(typeof(AssemblyInformationalVersionAttribute).GetConstructor([typeof(string)])!, ["1.2.3-test+abc"]));

        // Act
        var actual = ProductVersion.Read(assembly);

        // Assert
        Assert.AreEqual("1.2.3-test+abc", actual, "The informational version should be returned exactly as stamped");
    }

    /// <summary>
    /// An assembly without the attribute gives the fallback version.
    /// </summary>
    [TestMethod]
    public void ProductVersionReadWithoutAttributeReturnsFallback()
    {
        // Arrange
        var assembly = CreateAssembly("WithoutAttribute");

        // Act
        var actual = ProductVersion.Read(assembly);

        // Assert
        Assert.AreEqual("0.0.0", actual, "An assembly without an informational version should give 0.0.0");
    }

    /// <summary>
    /// The informational version of this solution's assembly equals the build version.
    /// </summary>
    [TestMethod]
    public void ProductVersionReadInformationalVersionEqualsBuildVersion()
    {
        // Arrange
        var assembly = typeof(ProductVersion).Assembly;
        var expected = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                               .Single(attribute => attribute.Key == "CensioBuildVersion")
                               .Value;

        // Act
        var actual = ProductVersion.Read(assembly);

        // Assert
        Assert.AreEqual(expected, actual, "The informational version should equal the build version without a source revision suffix");
    }

    /// <summary>
    /// Creates a dynamic assembly with the given attributes.
    /// </summary>
    /// <param name="name">The assembly name</param>
    /// <param name="attributes">The custom attributes to stamp</param>
    /// <returns>The dynamic assembly</returns>
    private static AssemblyBuilder CreateAssembly(string name, params CustomAttributeBuilder[] attributes)
    {
        return AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(name), AssemblyBuilderAccess.Run, attributes);
    }

    #endregion // Methods
}