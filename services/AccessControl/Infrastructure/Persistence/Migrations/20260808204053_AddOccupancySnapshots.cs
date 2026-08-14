using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VenuePulse.AccessControl.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOccupancySnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "occupancy_snapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    ZoneId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConfirmedSold = table.Column<int>(type: "integer", nullable: false),
                    CheckedIn = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_occupancy_snapshots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_occupancy_snapshots_EventId_RecordedAtUtc",
                table: "occupancy_snapshots",
                columns: new[] { "EventId", "RecordedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "occupancy_snapshots");
        }
    }
}
