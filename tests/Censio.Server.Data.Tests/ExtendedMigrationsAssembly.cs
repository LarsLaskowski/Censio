using System.Reflection;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Censio.Server.Data.Tests;

/// <summary>
/// An <see cref="IMigrationsAssembly" /> of the real migrations of <c>Censio.Server.Data</c> plus the test migrations
/// named in <see cref="ExtendedMigrationsOptionsExtension" />, for a test-only schema history.
/// </summary>
public sealed class ExtendedMigrationsAssembly : IMigrationsAssembly
{
    #region Constants

    /// <summary>
    /// The length of the timestamp and the separator in front of a migration's name in its identifier.
    /// </summary>
    private const int MigrationIdPrefixLength = 15;

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The migrations by identifier, real ones and test ones.
    /// </summary>
    private readonly SortedDictionary<string, TypeInfo> _migrations;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ExtendedMigrationsAssembly" /> class.
    /// </summary>
    /// <param name="options">The options of the context, carrying the <see cref="ExtendedMigrationsOptionsExtension" /></param>
    public ExtendedMigrationsAssembly(IDbContextOptions options)
    {
        var testMigrations = options.FindExtension<ExtendedMigrationsOptionsExtension>()?.TestMigrations ?? [];

        _migrations = new SortedDictionary<string, TypeInfo>(FindMigrations(RealAssembly).Concat(testMigrations.Select(type => type.GetTypeInfo()))
                                                                                         .ToDictionary(migration => ReadMigrationId(migration), StringComparer.Ordinal),
                                                             StringComparer.Ordinal);

        ModelSnapshot = CreateModelSnapshot(RealAssembly);
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the assembly of the real migrations.
    /// </summary>
    public static Assembly RealAssembly => typeof(ServerDataAssembly).Assembly;

    /// <summary>
    /// Gets the identifiers of the real migrations, in the order they are applied.
    /// </summary>
    public static IReadOnlyList<string> RealMigrationIds => FindMigrations(RealAssembly).Select(ReadMigrationId)
                                                                                        .Order(StringComparer.Ordinal)
                                                                                        .ToList();

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Configures a context to use the real migrations plus the given test migrations.
    /// </summary>
    /// <param name="builder">The options builder of the context</param>
    /// <param name="testMigrations">The test migration classes, each carrying a <see cref="MigrationAttribute" /></param>
    public static void Configure(DbContextOptionsBuilder builder, IReadOnlyList<Type> testMigrations)
    {
        builder.ReplaceService<IMigrationsAssembly, ExtendedMigrationsAssembly>();

        ((IDbContextOptionsBuilderInfrastructure)builder).AddOrUpdateExtension(new ExtendedMigrationsOptionsExtension(testMigrations));
    }

    /// <summary>
    /// Reads the identifier of a migration class.
    /// </summary>
    /// <param name="migration">The migration class</param>
    /// <returns>The identifier its <see cref="MigrationAttribute" /> names</returns>
    public static string ReadMigrationId(Type migration)
    {
        return migration.GetCustomAttribute<MigrationAttribute>()?.Id ?? throw new InvalidOperationException($"The migration class {migration.Name} carries no migration attribute");
    }

    /// <summary>
    /// Finds the migration classes of an assembly.
    /// </summary>
    /// <param name="assembly">The assembly</param>
    /// <returns>Every non-abstract migration class with a <see cref="MigrationAttribute" /></returns>
    private static IEnumerable<TypeInfo> FindMigrations(Assembly assembly)
    {
        return assembly.DefinedTypes.Where(type => type.IsAbstract == false
                                                   && type.IsSubclassOf(typeof(Migration))
                                                   && type.GetCustomAttribute<MigrationAttribute>() is not null);
    }

    /// <summary>
    /// Creates the model snapshot of an assembly.
    /// </summary>
    /// <param name="assembly">The assembly</param>
    /// <returns>The snapshot, or <see langword="null" /> when the assembly holds none</returns>
    private static ModelSnapshot? CreateModelSnapshot(Assembly assembly)
    {
        var snapshotType = assembly.DefinedTypes.SingleOrDefault(type => type.IsAbstract == false && type.IsSubclassOf(typeof(ModelSnapshot)));

        return snapshotType is null
                   ? null
                   : (ModelSnapshot?)Activator.CreateInstance(snapshotType, true);
    }

    #endregion // Methods

    #region IMigrationsAssembly

    /// <inheritdoc />
    public IReadOnlyDictionary<string, TypeInfo> Migrations => _migrations;

    /// <inheritdoc />
    public ModelSnapshot? ModelSnapshot { get; }

    /// <inheritdoc />
    public Assembly Assembly => RealAssembly;

    /// <inheritdoc />
    public string? FindMigrationId(string nameOrId)
    {
        return _migrations.Keys.FirstOrDefault(id => string.Equals(id, nameOrId, StringComparison.OrdinalIgnoreCase)
                                                     || string.Equals(id[MigrationIdPrefixLength..], nameOrId, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public Migration CreateMigration(TypeInfo migrationClass, string activeProvider)
    {
        var migration = (Migration)Activator.CreateInstance(migrationClass.AsType(), true)!;

        migration.ActiveProvider = activeProvider;

        return migration;
    }

    /// <inheritdoc />
    public void AddMigrations(Assembly additionalMigrationsAssembly)
    {
        throw new NotSupportedException("The test migrations are named through ExtendedMigrationsOptionsExtension");
    }

    #endregion // IMigrationsAssembly
}