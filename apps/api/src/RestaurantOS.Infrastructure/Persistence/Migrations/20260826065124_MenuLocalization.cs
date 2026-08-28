using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MenuLocalization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MenuCategoryTranslations",
                schema: "customer",
                columns: table => new
                {
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Locale = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuCategoryTranslations", x => new { x.CategoryId, x.Locale });
                });

            migrationBuilder.CreateTable(
                name: "MenuItemTranslations",
                schema: "customer",
                columns: table => new
                {
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Locale = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuItemTranslations", x => new { x.ItemId, x.Locale });
                });

            migrationBuilder.CreateIndex(
                name: "IX_MenuCategoryTranslations_TenantId_BranchId_CategoryId",
                schema: "customer",
                table: "MenuCategoryTranslations",
                columns: new[] { "TenantId", "BranchId", "CategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemTranslations_TenantId_BranchId_ItemId",
                schema: "customer",
                table: "MenuItemTranslations",
                columns: new[] { "TenantId", "BranchId", "ItemId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MenuCategoryTranslations",
                schema: "customer");

            migrationBuilder.DropTable(
                name: "MenuItemTranslations",
                schema: "customer");
        }
    }
}
