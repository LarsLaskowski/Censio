using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Censio.Server.Data.Migrations;

/// <inheritdoc />
public partial class AddHistory : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(name: "DeletedProfiles",
                                     columns: table => new
                                                       {
                                                           TombstoneId = table.Column<Guid>(type: "TEXT", nullable: false)
                                                       },
                                     constraints: table =>
                                                  {
                                                      table.PrimaryKey("PK_DeletedProfiles", x => x.TombstoneId);
                                                  });

        migrationBuilder.CreateTable(name: "FoldedTicks",
                                     columns: table => new
                                                       {
                                                           Number = table.Column<long>(type: "INTEGER", nullable: false)
                                                                         .Annotation("Sqlite:Autoincrement", true),
                                                           Id = table.Column<Guid>(type: "TEXT", nullable: false),
                                                           PointNumber = table.Column<long>(type: "INTEGER", nullable: false)
                                                       },
                                     constraints: table =>
                                                  {
                                                      table.PrimaryKey("PK_FoldedTicks", x => x.Number);
                                                  });

        migrationBuilder.CreateTable(name: "HourlyEntries",
                                     columns: table => new
                                                       {
                                                           Id = table.Column<long>(type: "INTEGER", nullable: false)
                                                                     .Annotation("Sqlite:Autoincrement", true),
                                                           HourlyIslandId = table.Column<long>(type: "INTEGER", nullable: false),
                                                           ProductGuid = table.Column<int>(type: "INTEGER", nullable: false),
                                                           EntryCount = table.Column<int>(type: "INTEGER", nullable: false),
                                                           GenerationSum = table.Column<double>(type: "REAL", nullable: false),
                                                           GenerationCount = table.Column<int>(type: "INTEGER", nullable: false),
                                                           ConsumptionSum = table.Column<double>(type: "REAL", nullable: false),
                                                           ConsumptionCount = table.Column<int>(type: "INTEGER", nullable: false),
                                                           DeltaSum = table.Column<double>(type: "REAL", nullable: false),
                                                           DeltaCount = table.Column<int>(type: "INTEGER", nullable: false),
                                                           PerfectGenerationSum = table.Column<double>(type: "REAL", nullable: false),
                                                           PerfectGenerationCount = table.Column<int>(type: "INTEGER", nullable: false),
                                                           PerfectConsumptionSum = table.Column<double>(type: "REAL", nullable: false),
                                                           PerfectConsumptionCount = table.Column<int>(type: "INTEGER", nullable: false),
                                                           IncomeSum = table.Column<double>(type: "REAL", nullable: false),
                                                           IncomeCount = table.Column<int>(type: "INTEGER", nullable: false),
                                                           SummedProductivitySum = table.Column<double>(type: "REAL", nullable: false),
                                                           SummedProductivityCount = table.Column<int>(type: "INTEGER", nullable: false),
                                                           AverageProductivitySum = table.Column<double>(type: "REAL", nullable: false),
                                                           AverageProductivityCount = table.Column<int>(type: "INTEGER", nullable: false),
                                                           BuildingCountSum = table.Column<long>(type: "INTEGER", nullable: false),
                                                           MaintenanceSum = table.Column<long>(type: "INTEGER", nullable: false),
                                                           ProfitSum = table.Column<long>(type: "INTEGER", nullable: false)
                                                       },
                                     constraints: table =>
                                                  {
                                                      table.PrimaryKey("PK_HourlyEntries", x => x.Id);
                                                  });

        migrationBuilder.CreateTable(name: "HourlyIslands",
                                     columns: table => new
                                                       {
                                                           Id = table.Column<long>(type: "INTEGER", nullable: false)
                                                                     .Annotation("Sqlite:Autoincrement", true),
                                                           PointNumber = table.Column<long>(type: "INTEGER", nullable: false),
                                                           SessionGuid = table.Column<int>(type: "INTEGER", nullable: false),
                                                           IslandId = table.Column<byte>(type: "INTEGER", nullable: false),
                                                           AreaIndex = table.Column<byte>(type: "INTEGER", nullable: false),
                                                           Name = table.Column<string>(type: "TEXT", nullable: false),
                                                           NameReceiveTicks = table.Column<long>(type: "INTEGER", nullable: false),
                                                           NameTickNumber = table.Column<long>(type: "INTEGER", nullable: false)
                                                       },
                                     constraints: table =>
                                                  {
                                                      table.PrimaryKey("PK_HourlyIslands", x => x.Id);
                                                  });

        migrationBuilder.CreateTable(name: "HourlyMapPairs",
                                     columns: table => new
                                                       {
                                                           Id = table.Column<long>(type: "INTEGER", nullable: false)
                                                                     .Annotation("Sqlite:Autoincrement", true),
                                                           EntryId = table.Column<long>(type: "INTEGER", nullable: false),
                                                           Kind = table.Column<byte>(type: "INTEGER", nullable: false),
                                                           Guid = table.Column<int>(type: "INTEGER", nullable: false),
                                                           AmountSum = table.Column<long>(type: "INTEGER", nullable: false)
                                                       },
                                     constraints: table =>
                                                  {
                                                      table.PrimaryKey("PK_HourlyMapPairs", x => x.Id);
                                                  });

        migrationBuilder.CreateTable(name: "HourlyPoints",
                                     columns: table => new
                                                       {
                                                           Number = table.Column<long>(type: "INTEGER", nullable: false)
                                                                         .Annotation("Sqlite:Autoincrement", true),
                                                           ProfileId = table.Column<Guid>(type: "TEXT", nullable: false),
                                                           HourTicks = table.Column<long>(type: "INTEGER", nullable: false),
                                                           TickCount = table.Column<int>(type: "INTEGER", nullable: false),
                                                           FirstGameTime = table.Column<long>(type: "INTEGER", nullable: false),
                                                           LastGameTime = table.Column<long>(type: "INTEGER", nullable: false)
                                                       },
                                     constraints: table =>
                                                  {
                                                      table.PrimaryKey("PK_HourlyPoints", x => x.Number);
                                                  });

        migrationBuilder.CreateIndex(name: "IX_FoldedTicks_Id",
                                     table: "FoldedTicks",
                                     column: "Id",
                                     unique: true);

        migrationBuilder.CreateIndex(name: "IX_FoldedTicks_PointNumber",
                                     table: "FoldedTicks",
                                     column: "PointNumber");

        migrationBuilder.CreateIndex(name: "IX_HourlyEntries_HourlyIslandId_ProductGuid",
                                     table: "HourlyEntries",
                                     columns: new[] { "HourlyIslandId", "ProductGuid" },
                                     unique: true);

        migrationBuilder.CreateIndex(name: "IX_HourlyIslands_PointNumber_SessionGuid_IslandId_AreaIndex",
                                     table: "HourlyIslands",
                                     columns: new[] { "PointNumber", "SessionGuid", "IslandId", "AreaIndex" },
                                     unique: true);

        migrationBuilder.CreateIndex(name: "IX_HourlyMapPairs_EntryId_Kind_Guid",
                                     table: "HourlyMapPairs",
                                     columns: new[] { "EntryId", "Kind", "Guid" },
                                     unique: true);

        migrationBuilder.CreateIndex(name: "IX_HourlyPoints_ProfileId_HourTicks",
                                     table: "HourlyPoints",
                                     columns: new[] { "ProfileId", "HourTicks" },
                                     unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "DeletedProfiles");

        migrationBuilder.DropTable(name: "FoldedTicks");

        migrationBuilder.DropTable(name: "HourlyEntries");

        migrationBuilder.DropTable(name: "HourlyIslands");

        migrationBuilder.DropTable(name: "HourlyMapPairs");

        migrationBuilder.DropTable(name: "HourlyPoints");
    }
}