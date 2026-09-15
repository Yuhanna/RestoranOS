using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RestaurantOS.Infrastructure;

#nullable disable

namespace RestaurantOS.Infrastructure.Persistence.Migrations;

[DbContext(typeof(RestaurantOsDbContext))]
[Migration("20260915130000_AddOrderItemSourcePackage")]
public partial class AddOrderItemSourcePackage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "SourcePackageId",
            schema: "customer",
            table: "CustomerOrderItems",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SourcePackageName",
            schema: "customer",
            table: "CustomerOrderItems",
            type: "nvarchar(160)",
            maxLength: 160,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "SourcePackageId",
            schema: "customer",
            table: "CustomerOrderItems");

        migrationBuilder.DropColumn(
            name: "SourcePackageName",
            schema: "customer",
            table: "CustomerOrderItems");
    }
}
