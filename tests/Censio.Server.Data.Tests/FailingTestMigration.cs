using Microsoft.EntityFrameworkCore.Migrations;

namespace Censio.Server.Data.Tests;

/// <summary>
/// A test-only schema step after <see cref="AddedTableTestMigration" /> whose command fails in SQLite.
/// </summary>
[Migration(Id)]
public sealed class FailingTestMigration : Migration
{
    #region Constants

    /// <summary>
    /// The migration identifier.
    /// </summary>
    public const string Id = "29990101000001_FailingTest";

    #endregion // Constants

    #region Migration

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("INSERT INTO \"MissingTestTable\" (\"Id\") VALUES (1);");
    }

    #endregion // Migration
}