using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VenuePulse.AccessControl.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEventIdToZone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "EventId",
                table: "zones",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_zones_EventId",
                table: "zones",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_zones_EventId_Name",
                table: "zones",
                columns: new[] { "EventId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_zones_EventId",
                table: "zones");

            migrationBuilder.DropIndex(
                name: "IX_zones_EventId_Name",
                table: "zones");

            migrationBuilder.DropColumn(
                name: "EventId",
                table: "zones");
        }
    }
}
