using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VenuePulse.AccessControl.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddZoneCancelled : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Cancelled",
                table: "zones",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Cancelled",
                table: "zones");
        }
    }
}
