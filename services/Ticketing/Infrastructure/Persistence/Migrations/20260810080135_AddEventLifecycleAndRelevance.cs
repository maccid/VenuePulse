using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VenuePulse.Ticketing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEventLifecycleAndRelevance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Relevance",
                table: "events",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Standard");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "events",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "OnSale");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Relevance",
                table: "events");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "events");
        }
    }
}
