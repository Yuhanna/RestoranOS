using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RestaurantOS.Infrastructure;

#nullable disable

namespace RestaurantOS.Infrastructure.Persistence.Migrations;

[DbContext(typeof(RestaurantOsDbContext))]
[Migration("20260904120000_BranchBillingAndFreeze")]
public partial class BranchBillingAndFreeze : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsFrozen",
            schema: "customer",
            table: "Branches",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<int>(
            name: "PurchasedBranchAddonCount",
            schema: "billing",
            table: "TenantSubscriptions",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.CreateTable(
            name: "EnterpriseQuoteRequests",
            schema: "billing",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ContactName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                Phone = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                EstimatedBranchCount = table.Column<int>(type: "int", nullable: false),
                Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EnterpriseQuoteRequests", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_EnterpriseQuoteRequests_TenantId_CreatedAtUtc",
            schema: "billing",
            table: "EnterpriseQuoteRequests",
            columns: new[] { "TenantId", "CreatedAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "EnterpriseQuoteRequests",
            schema: "billing");

        migrationBuilder.DropColumn(
            name: "PurchasedBranchAddonCount",
            schema: "billing",
            table: "TenantSubscriptions");

        migrationBuilder.DropColumn(
            name: "IsFrozen",
            schema: "customer",
            table: "Branches");
    }
}
