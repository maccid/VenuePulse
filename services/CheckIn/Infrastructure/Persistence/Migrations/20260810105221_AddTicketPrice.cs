using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VenuePulse.CheckIn.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketPrice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Price",
                table: "tickets",
                type: "numeric(10,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Price",
                table: "tickets");
        }
    }
}
