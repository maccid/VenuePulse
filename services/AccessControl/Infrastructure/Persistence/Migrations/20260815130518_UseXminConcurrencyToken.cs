using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VenuePulse.AccessControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UseXminConcurrencyToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "zones");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "zones",
                type: "bytea",
                rowVersion: true,
                nullable: true);
        }
    }
}
