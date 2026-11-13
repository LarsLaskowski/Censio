using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Censio.Server.Data.Tests;

/// <summary>
/// The metadata of an <see cref="ExtendedMigrationsOptionsExtension" />.
/// </summary>
public sealed class ExtendedMigrationsOptionsExtensionInfo : DbContextOptionsExtensionInfo
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ExtendedMigrationsOptionsExtensionInfo" /> class.
    /// </summary>
    /// <param name="extension">The extension described</param>
    public ExtendedMigrationsOptionsExtensionInfo(ExtendedMigrationsOptionsExtension extension)
        : base(extension)
    {
    }

    #endregion // Constructors

    #region DbContextOptionsExtensionInfo

    /// <inheritdoc />
    public override bool IsDatabaseProvider => false;

    /// <inheritdoc />
    public override string LogFragment => "ExtendedMigrations ";

    /// <inheritdoc />
    public override int GetServiceProviderHashCode()
    {
        return 0;
    }

    /// <inheritdoc />
    public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other)
    {
        return other is ExtendedMigrationsOptionsExtensionInfo;
    }

    /// <inheritdoc />
    public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
    {
        debugInfo["ExtendedMigrations:TestMigrations"] = string.Join(',', ((ExtendedMigrationsOptionsExtension)Extension).TestMigrations.Select(type => type.Name));
    }

    #endregion // DbContextOptionsExtensionInfo
}