using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VenuePulse.Ticketing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddZoneDemandSnapshotName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "zone_demand_snapshots",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Name",
                table: "zone_demand_snapshots");
        }
    }
}
