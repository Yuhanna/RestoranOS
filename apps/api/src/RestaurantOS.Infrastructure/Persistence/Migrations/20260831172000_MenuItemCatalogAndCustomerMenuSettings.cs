using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RestaurantOS.Infrastructure;

#nullable disable

namespace RestaurantOS.Infrastructure.Persistence.Migrations;

[DbContext(typeof(RestaurantOsDbContext))]
[Migration("20260831172000_MenuItemCatalogAndCustomerMenuSettings")]
public partial class MenuItemCatalogAndCustomerMenuSettings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "CatalogJson",
            schema: "customer",
            table: "MenuItems",
            type: "nvarchar(max)",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "CustomerMenuSettingsJson",
            schema: "customer",
            table: "Branches",
            type: "nvarchar(max)",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "CatalogJson",
            schema: "customer",
            table: "MenuItems");

        migrationBuilder.DropColumn(
            name: "CustomerMenuSettingsJson",
            schema: "customer",
            table: "Branches");
    }
}
