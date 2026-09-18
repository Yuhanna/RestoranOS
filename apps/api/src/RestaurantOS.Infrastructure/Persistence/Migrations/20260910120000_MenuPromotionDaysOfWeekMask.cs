using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RestaurantOS.Infrastructure;

#nullable disable

namespace RestaurantOS.Infrastructure.Persistence.Migrations;

[DbContext(typeof(RestaurantOsDbContext))]
[Migration("20260910120000_MenuPromotionDaysOfWeekMask")]
public partial class MenuPromotionDaysOfWeekMask : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<byte>(
            name: "DaysOfWeekMask",
            schema: "customer",
            table: "MenuPromotions",
            type: "tinyint",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "DaysOfWeekMask",
            schema: "customer",
            table: "MenuPromotions");
    }
}
