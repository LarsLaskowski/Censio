using Microsoft.EntityFrameworkCore;

namespace Censio.Server.Data.Tests;

/// <summary>
/// Tests for <see cref="CensioDbContextFactory" />.
/// </summary>
[TestClass]
public sealed class CensioDbContextFactoryTests
{
    #region Methods

    /// <summary>
    /// The model of the context the design-time tools create matches the model snapshot of the migrations, so no model
    /// change lacks its migration.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void CensioDbContextFactoryCreateDbContextHasNoPendingModelChanges()
    {
        // Arrange
        var factory = new CensioDbContextFactory();

        // Act
        using var context = factory.CreateDbContext([]);

        // Assert
        Assert.IsFalse(context.Database.HasPendingModelChanges(), "The model should match the migrations' snapshot; add a migration for every model change");
    }

    #endregion // Methods
}