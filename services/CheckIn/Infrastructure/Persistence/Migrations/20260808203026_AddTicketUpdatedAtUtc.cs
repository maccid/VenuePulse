using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VenuePulse.CheckIn.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketUpdatedAtUtc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAtUtc",
                table: "tickets",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "tickets");
        }
    }
}
