using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Censio.Server.Data.Tests;

/// <summary>
/// The context option that names the test migrations <see cref="ExtendedMigrationsAssembly" /> adds to the real ones.
/// </summary>
public sealed class ExtendedMigrationsOptionsExtension : IDbContextOptionsExtension
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ExtendedMigrationsOptionsExtension" /> class.
    /// </summary>
    /// <param name="testMigrations">The test migration classes</param>
    public ExtendedMigrationsOptionsExtension(IReadOnlyList<Type> testMigrations)
    {
        TestMigrations = testMigrations;
        Info = new ExtendedMigrationsOptionsExtensionInfo(this);
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the test migration classes.
    /// </summary>
    public IReadOnlyList<Type> TestMigrations { get; }

    #endregion // Properties

    #region IDbContextOptionsExtension

    /// <inheritdoc />
    public DbContextOptionsExtensionInfo Info { get; }

    /// <inheritdoc />
    public void ApplyServices(IServiceCollection services)
    {
        // The migrations assembly is replaced through ReplaceService; this option adds no service
    }

    /// <inheritdoc />
    public void Validate(IDbContextOptions options)
    {
        // Every list of test migrations is valid
    }

    #endregion // IDbContextOptionsExtension
}