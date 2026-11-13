using Microsoft.EntityFrameworkCore.Migrations;

namespace Censio.Server.Data.Tests;

/// <summary>
/// A test-only schema step after every real migration whose command runs outside the migration transaction.
/// </summary>
[Migration(Id)]
public sealed class SuppressedTransactionTestMigration : Migration
{
    #region Constants

    /// <summary>
    /// The migration identifier.
    /// </summary>
    public const string Id = "29990101000002_SuppressedTransactionTest";

    /// <summary>
    /// The name of the table the step would add.
    /// </summary>
    public const string TableName = "SuppressedTestTable";

    #endregion // Constants

    #region Migration

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql($"CREATE TABLE \"{TableName}\" (\"Id\" INTEGER NOT NULL PRIMARY KEY);", true);
    }

    #endregion // Migration
}