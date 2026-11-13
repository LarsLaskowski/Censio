using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Censio.Server.Data.Migrations;

/// <inheritdoc />
public partial class InitialSchema : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(name: "Catalog",
                                     columns: table => new
                                                       {
                                                           Id = table.Column<int>(type: "INTEGER", nullable: false),
                                                           Identity = table.Column<string>(type: "TEXT", nullable: false),
                                                           Document = table.Column<byte[]>(type: "BLOB", nullable: false)
                                                       },
                                     constraints: table =>
                                                  {
                                                      table.PrimaryKey("PK_Catalog", x => x.Id);
                                                  });

        migrationBuilder.CreateTable(name: "Icons",
                                     columns: table => new
                                                       {
                                                           Reference = table.Column<string>(type: "TEXT", nullable: false),
                                                           Png = table.Column<byte[]>(type: "BLOB", nullable: false)
                                                       },
                                     constraints: table =>
                                                  {
                                                      table.PrimaryKey("PK_Icons", x => x.Reference);
                                                  });

        migrationBuilder.CreateTable(name: "Islands",
                                     columns: table => new
                                                       {
                                                           Id = table.Column<long>(type: "INTEGER", nullable: false)
                                                                     .Annotation("Sqlite:Autoincrement", true),
                                                           ProfileId = table.Column<Guid>(type: "TEXT", nullable: false),
                                                           SessionGuid = table.Column<int>(type: "INTEGER", nullable: false),
                                                           IslandId = table.Column<byte>(type: "INTEGER", nullable: false),
                                                           AreaIndex = table.Column<byte>(type: "INTEGER", nullable: false),
                                                           Name = table.Column<string>(type: "TEXT", nullable: false),
                                                           NameReceiveTicks = table.Column<long>(type: "INTEGER", nullable: false),
                                                           NameTickNumber = table.Column<long>(type: "INTEGER", nullable: false)
                                                       },
                                     constraints: table =>
                                                  {
                                                      table.PrimaryKey("PK_Islands", x => x.Id);
                                                  });

        migrationBuilder.CreateTable(name: "Profiles",
                                     columns: table => new
                                                       {
                                                           Id = table.Column<Guid>(type: "TEXT", nullable: false),
                                                           Name = table.Column<string>(type: "TEXT", nullable: true)
                                                       },
                                     constraints: table =>
                                                  {
                                                      table.PrimaryKey("PK_Profiles", x => x.Id);
                                                  });

        migrationBuilder.CreateTable(name: "TickEntries",
                                     columns: table => new
                                                       {
                                                           Id = table.Column<long>(type: "INTEGER", nullable: false)
                                                                     .Annotation("Sqlite:Autoincrement", true),
                                                           TickIslandId = table.Column<long>(type: "INTEGER", nullable: false),
                                                           Position = table.Column<int>(type: "INTEGER", nullable: false),
                                                           ProductGuid = table.Column<int>(type: "INTEGER", nullable: false),
                                                           Generation = table.Column<int>(type: "INTEGER", nullable: true),
                                                           Consumption = table.Column<int>(type: "INTEGER", nullable: true),
                                                           Delta = table.Column<int>(type: "INTEGER", nullable: true),
                                                           PerfectGeneration = table.Column<int>(type: "INTEGER", nullable: true),
                                                           PerfectConsumption = table.Column<int>(type: "INTEGER", nullable: true),
                                                           Income = table.Column<int>(type: "INTEGER", nullable: true),
                                                           SummedProductivity = table.Column<int>(type: "INTEGER", nullable: true),
                                                           AverageProductivity = table.Column<int>(type: "INTEGER", nullable: true),
                                                           BuildingCount = table.Column<int>(type: "INTEGER", nullable: false),
                                                           Maintenance = table.Column<int>(type: "INTEGER", nullable: false),
                                                           Profit = table.Column<int>(type: "INTEGER", nullable: false)
                                                       },
                                     constraints: table =>
                                                  {
                                                      table.PrimaryKey("PK_TickEntries", x => x.Id);
                                                  });

        migrationBuilder.CreateTable(name: "TickIslands",
                                     columns: table => new
                                                       {
                                                           Id = table.Column<long>(type: "INTEGER", nullable: false)
                                                                     .Annotation("Sqlite:Autoincrement", true),
                                                           TickNumber = table.Column<long>(type: "INTEGER", nullable: false),
                                                           Position = table.Column<int>(type: "INTEGER", nullable: false),
                                                           SessionGuid = table.Column<int>(type: "INTEGER", nullable: false),
                                                           IslandId = table.Column<byte>(type: "INTEGER", nullable: false),
                                                           AreaIndex = table.Column<byte>(type: "INTEGER", nullable: false),
                                                           SessionId = table.Column<byte>(type: "INTEGER", nullable: false),
                                                           Name = table.Column<string>(type: "TEXT", nullable: false)
                                                       },
                                     constraints: table =>
                                                  {
                                                      table.PrimaryKey("PK_TickIslands", x => x.Id);
                                                  });

        migrationBuilder.CreateTable(name: "TickMapPairs",
                                     columns: table => new
                                                       {
                                                           Id = table.Column<long>(type: "INTEGER", nullable: false)
                                                                     .Annotation("Sqlite:Autoincrement", true),
                                                           EntryId = table.Column<long>(type: "INTEGER", nullable: false),
                                                           Kind = table.Column<byte>(type: "INTEGER", nullable: false),
                                                           Guid = table.Column<int>(type: "INTEGER", nullable: false),
                                                           Amount = table.Column<int>(type: "INTEGER", nullable: false)
                                                       },
                                     constraints: table =>
                                                  {
                                                      table.PrimaryKey("PK_TickMapPairs", x => x.Id);
                                                  });

        migrationBuilder.CreateTable(name: "Ticks",
                                     columns: table => new
                                                       {
                                                           Number = table.Column<long>(type: "INTEGER", nullable: false)
                                                                         .Annotation("Sqlite:Autoincrement", true),
                                                           Id = table.Column<Guid>(type: "TEXT", nullable: false),
                                                           ProfileId = table.Column<Guid>(type: "TEXT", nullable: false),
                                                           ReceiveTicks = table.Column<long>(type: "INTEGER", nullable: false),
                                                           GameTime = table.Column<long>(type: "INTEGER", nullable: false)
                                                       },
                                     constraints: table =>
                                                  {
                                                      table.PrimaryKey("PK_Ticks", x => x.Number);
                                                  });

        migrationBuilder.CreateIndex(name: "IX_Islands_ProfileId_SessionGuid_IslandId_AreaIndex",
                                     table: "Islands",
                                     columns: new[] { "ProfileId", "SessionGuid", "IslandId", "AreaIndex" },
                                     unique: true);

        migrationBuilder.CreateIndex(name: "IX_TickEntries_TickIslandId_Position",
                                     table: "TickEntries",
                                     columns: new[] { "TickIslandId", "Position" },
                                     unique: true);

        migrationBuilder.CreateIndex(name: "IX_TickIslands_TickNumber_Position",
                                     table: "TickIslands",
                                     columns: new[] { "TickNumber", "Position" },
                                     unique: true);

        migrationBuilder.CreateIndex(name: "IX_TickMapPairs_EntryId",
                                     table: "TickMapPairs",
                                     column: "EntryId");

        migrationBuilder.CreateIndex(name: "IX_Ticks_Id",
                                     table: "Ticks",
                                     column: "Id",
                                     unique: true);

        migrationBuilder.CreateIndex(name: "IX_Ticks_ProfileId_ReceiveTicks_Number",
                                     table: "Ticks",
                                     columns: new[] { "ProfileId", "ReceiveTicks", "Number" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Catalog");

        migrationBuilder.DropTable(name: "Icons");

        migrationBuilder.DropTable(name: "Islands");

        migrationBuilder.DropTable(name: "Profiles");

        migrationBuilder.DropTable(name: "TickEntries");

        migrationBuilder.DropTable(name: "TickIslands");

        migrationBuilder.DropTable(name: "TickMapPairs");

        migrationBuilder.DropTable(name: "Ticks");
    }
}