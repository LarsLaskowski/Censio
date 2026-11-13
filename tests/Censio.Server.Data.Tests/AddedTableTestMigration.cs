using Microsoft.EntityFrameworkCore.Migrations;

namespace Censio.Server.Data.Tests;

/// <summary>
/// A test-only schema step after every real migration: it adds the table <see cref="TableName" />.
/// </summary>
[Migration(Id)]
public sealed class AddedTableTestMigration : Migration
{
    #region Constants

    /// <summary>
    /// The migration identifier.
    /// </summary>
    public const string Id = "29990101000000_AddedTableTest";

    /// <summary>
    /// The name of the table the step adds.
    /// </summary>
    public const string TableName = "AddedTestTable";

    #endregion // Constants

    #region Migration

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql($"CREATE TABLE \"{TableName}\" (\"Id\" INTEGER NOT NULL PRIMARY KEY);");
    }

    #endregion // Migration
}