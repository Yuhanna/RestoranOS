using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TableQrManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProtectedToken",
                schema: "customer",
                table: "TableQrCodes",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProtectedToken",
                schema: "customer",
                table: "TableQrCodes");
        }
    }
}
