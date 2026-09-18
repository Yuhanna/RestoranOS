using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RestaurantOS.Infrastructure;

#nullable disable

namespace RestaurantOS.Infrastructure.Persistence.Migrations;

[DbContext(typeof(RestaurantOsDbContext))]
[Migration("20260908120000_ManagementUserProfileFields")]
public partial class ManagementUserProfileFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "DisplayName",
            schema: "auth",
            table: "Users",
            type: "nvarchar(120)",
            maxLength: 120,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Phone",
            schema: "auth",
            table: "Users",
            type: "nvarchar(40)",
            maxLength: 40,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "DisplayName",
            schema: "auth",
            table: "Users");

        migrationBuilder.DropColumn(
            name: "Phone",
            schema: "auth",
            table: "Users");
    }
}
