using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Censio.Server.Data.Migrations;

/// <inheritdoc />
public partial class AddTickEntryProductIndex : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(name: "IX_TickEntries_TickIslandId_ProductGuid_Position",
                                     table: "TickEntries",
                                     columns: new[] { "TickIslandId", "ProductGuid", "Position" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_TickEntries_TickIslandId_ProductGuid_Position",
                                   table: "TickEntries");
    }
}