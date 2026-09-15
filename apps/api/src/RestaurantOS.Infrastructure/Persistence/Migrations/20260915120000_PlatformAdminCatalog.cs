using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RestaurantOS.Infrastructure;

#nullable disable

namespace RestaurantOS.Infrastructure.Persistence.Migrations;

[DbContext(typeof(RestaurantOsDbContext))]
[Migration("20260915120000_PlatformAdminCatalog")]
public partial class PlatformAdminCatalog : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Realm",
            schema: "auth",
            table: "RefreshSessions",
            type: "nvarchar(16)",
            maxLength: 16,
            nullable: false,
            defaultValue: "management");

        migrationBuilder.CreateTable(
            name: "PlatformStaff",
            schema: "auth",
            columns: table => new
            {
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RoleCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                GrantedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PlatformStaff", x => x.UserId);
                table.ForeignKey(
                    name: "FK_PlatformStaff_Users_UserId",
                    column: x => x.UserId,
                    principalSchema: "auth",
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_PlatformStaff_IsActive_RoleCode",
            schema: "auth",
            table: "PlatformStaff",
            columns: new[] { "IsActive", "RoleCode" });

        migrationBuilder.CreateTable(
            name: "PlanPrices",
            schema: "billing",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProductCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                ProductKind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                Interval = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                Currency = table.Column<string>(type: "nchar(3)", fixedLength: true, maxLength: 3, nullable: false),
                AmountMinor = table.Column<long>(type: "bigint", nullable: false),
                TaxInclusive = table.Column<bool>(type: "bit", nullable: false),
                Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                PublishedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                ArchivedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PlanPrices", x => x.Id);
                table.ForeignKey(
                    name: "FK_PlanPrices_Users_CreatedByUserId",
                    column: x => x.CreatedByUserId,
                    principalSchema: "auth",
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_PlanPrices_CreatedByUserId",
            schema: "billing",
            table: "PlanPrices",
            column: "CreatedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_PlanPrices_ProductCode_Interval_Currency_Status_CreatedAtUtc",
            schema: "billing",
            table: "PlanPrices",
            columns: new[] { "ProductCode", "Interval", "Currency", "Status", "CreatedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "UX_PlanPrices_Published",
            schema: "billing",
            table: "PlanPrices",
            columns: new[] { "ProductCode", "Interval", "Currency" },
            unique: true,
            filter: "[Status] = N'published'");

        migrationBuilder.CreateTable(
            name: "MenuPackages",
            schema: "customer",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                PriceAmountMinor = table.Column<long>(type: "bigint", nullable: false),
                PriceCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                SortOrder = table.Column<int>(type: "int", nullable: false),
                DailyStartLocal = table.Column<TimeOnly>(type: "time", nullable: true),
                DailyEndLocal = table.Column<TimeOnly>(type: "time", nullable: true),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MenuPackages", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "MenuPackageComponents",
            schema: "customer",
            columns: table => new
            {
                MenuPackageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                MenuItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SlotLabel = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                SortOrder = table.Column<int>(type: "int", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MenuPackageComponents", x => new { x.MenuPackageId, x.MenuItemId });
                table.ForeignKey(
                    name: "FK_MenuPackageComponents_MenuPackages_MenuPackageId",
                    column: x => x.MenuPackageId,
                    principalSchema: "customer",
                    principalTable: "MenuPackages",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_MenuPackageComponents_MenuItems_MenuItemId",
                    column: x => x.MenuItemId,
                    principalSchema: "customer",
                    principalTable: "MenuItems",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_MenuPackageComponents_MenuItemId",
            schema: "customer",
            table: "MenuPackageComponents",
            column: "MenuItemId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "MenuPackageComponents",
            schema: "customer");

        migrationBuilder.DropTable(
            name: "MenuPackages",
            schema: "customer");

        migrationBuilder.DropTable(
            name: "PlanPrices",
            schema: "billing");

        migrationBuilder.DropTable(
            name: "PlatformStaff",
            schema: "auth");

        migrationBuilder.DropColumn(
            name: "Realm",
            schema: "auth",
            table: "RefreshSessions");
    }
}
