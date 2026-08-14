using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VenuePulse.CheckIn.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketGroupId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "GroupId",
                table: "tickets",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_tickets_GroupId",
                table: "tickets",
                column: "GroupId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_tickets_GroupId",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "tickets");
        }
    }
}
