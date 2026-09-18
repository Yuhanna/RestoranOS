using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantOS.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class MenuPackages : Migration
{
    /// <inheritdoc />
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
            type: "nvarchar(120)",
            maxLength: 120,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "MenuPackages",
            schema: "customer",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                PriceAmountMinor = table.Column<long>(type: "bigint", nullable: false),
                PriceCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                SortOrder = table.Column<int>(type: "int", nullable: false),
                DailyStartLocal = table.Column<TimeOnly>(type: "time", nullable: true),
                DailyEndLocal = table.Column<TimeOnly>(type: "time", nullable: true),
                DaysOfWeekMask = table.Column<byte>(type: "tinyint", nullable: true),
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
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PackageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                MenuItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SlotLabel = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                SortOrder = table.Column<int>(type: "int", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MenuPackageComponents", x => x.Id);
                table.ForeignKey(
                    name: "FK_MenuPackageComponents_MenuItems_MenuItemId",
                    column: x => x.MenuItemId,
                    principalSchema: "customer",
                    principalTable: "MenuItems",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_MenuPackageComponents_MenuPackages_PackageId",
                    column: x => x.PackageId,
                    principalSchema: "customer",
                    principalTable: "MenuPackages",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_CustomerOrderItems_SourcePackageId",
            schema: "customer",
            table: "CustomerOrderItems",
            column: "SourcePackageId");

        migrationBuilder.CreateIndex(
            name: "IX_MenuPackageComponents_MenuItemId",
            schema: "customer",
            table: "MenuPackageComponents",
            column: "MenuItemId");

        migrationBuilder.CreateIndex(
            name: "IX_MenuPackageComponents_PackageId_MenuItemId",
            schema: "customer",
            table: "MenuPackageComponents",
            columns: new[] { "PackageId", "MenuItemId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_MenuPackageComponents_PackageId_SortOrder",
            schema: "customer",
            table: "MenuPackageComponents",
            columns: new[] { "PackageId", "SortOrder" });

        migrationBuilder.CreateIndex(
            name: "IX_MenuPackages_TenantId_BranchId_IsActive_SortOrder",
            schema: "customer",
            table: "MenuPackages",
            columns: new[] { "TenantId", "BranchId", "IsActive", "SortOrder" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "MenuPackageComponents",
            schema: "customer");

        migrationBuilder.DropTable(
            name: "MenuPackages",
            schema: "customer");

        migrationBuilder.DropIndex(
            name: "IX_CustomerOrderItems_SourcePackageId",
            schema: "customer",
            table: "CustomerOrderItems");

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
