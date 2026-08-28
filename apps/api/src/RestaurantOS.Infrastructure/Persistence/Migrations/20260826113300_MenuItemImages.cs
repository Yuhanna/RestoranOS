using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantOS.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class MenuItemImages : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ImageAlt",
            schema: "customer",
            table: "MenuItems",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "ImageUrl",
            schema: "customer",
            table: "MenuItems",
            type: "nvarchar(2048)",
            maxLength: 2048,
            nullable: false,
            defaultValue: "");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ImageAlt",
            schema: "customer",
            table: "MenuItems");

        migrationBuilder.DropColumn(
            name: "ImageUrl",
            schema: "customer",
            table: "MenuItems");
    }
}
