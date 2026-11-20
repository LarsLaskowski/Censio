using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Censio.Server.Data.Migrations;

/// <inheritdoc />
public partial class AddTradeRoutes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(name: "TradeRoutes",
                                     columns: table => new
                                                       {
                                                           Id = table.Column<long>(type: "INTEGER", nullable: false)
                                                                     .Annotation("Sqlite:Autoincrement", true),
                                                           ProfileId = table.Column<Guid>(type: "TEXT", nullable: false),
                                                           Position = table.Column<int>(type: "INTEGER", nullable: false),
                                                           RouteId = table.Column<int>(type: "INTEGER", nullable: false),
                                                           Name = table.Column<string>(type: "TEXT", nullable: false),
                                                           Folder = table.Column<string>(type: "TEXT", nullable: false),
                                                           MajorProblems = table.Column<int>(type: "INTEGER", nullable: false),
                                                           MinorProblems = table.Column<int>(type: "INTEGER", nullable: false),
                                                           Ships = table.Column<int>(type: "INTEGER", nullable: false),
                                                           LostShips = table.Column<int>(type: "INTEGER", nullable: false)
                                                       },
                                     constraints: table =>
                                                  {
                                                      table.PrimaryKey("PK_TradeRoutes", x => x.Id);
                                                  });

        migrationBuilder.CreateTable(name: "TradeRouteSets",
                                     columns: table => new
                                                       {
                                                           ProfileId = table.Column<Guid>(type: "TEXT", nullable: false),
                                                           MessageId = table.Column<Guid>(type: "TEXT", nullable: false),
                                                           ReceiveTicks = table.Column<long>(type: "INTEGER", nullable: false)
                                                       },
                                     constraints: table =>
                                                  {
                                                      table.PrimaryKey("PK_TradeRouteSets", x => x.ProfileId);
                                                  });

        migrationBuilder.CreateTable(name: "TradeRouteStations",
                                     columns: table => new
                                                       {
                                                           Id = table.Column<long>(type: "INTEGER", nullable: false)
                                                                     .Annotation("Sqlite:Autoincrement", true),
                                                           TradeRouteId = table.Column<long>(type: "INTEGER", nullable: false),
                                                           Position = table.Column<int>(type: "INTEGER", nullable: false),
                                                           SessionGuid = table.Column<int>(type: "INTEGER", nullable: false),
                                                           IslandId = table.Column<byte>(type: "INTEGER", nullable: false),
                                                           AreaIndex = table.Column<byte>(type: "INTEGER", nullable: false)
                                                       },
                                     constraints: table =>
                                                  {
                                                      table.PrimaryKey("PK_TradeRouteStations", x => x.Id);
                                                  });

        migrationBuilder.CreateIndex(name: "IX_TradeRoutes_ProfileId_Position",
                                     table: "TradeRoutes",
                                     columns: new[] { "ProfileId", "Position" },
                                     unique: true);

        migrationBuilder.CreateIndex(name: "IX_TradeRouteStations_TradeRouteId_Position",
                                     table: "TradeRouteStations",
                                     columns: new[] { "TradeRouteId", "Position" },
                                     unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "TradeRoutes");

        migrationBuilder.DropTable(name: "TradeRouteSets");

        migrationBuilder.DropTable(name: "TradeRouteStations");
    }
}