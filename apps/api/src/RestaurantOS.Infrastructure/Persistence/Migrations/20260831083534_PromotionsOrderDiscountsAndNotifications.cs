using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PromotionsOrderDiscountsAndNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "DiscountAmountMinor",
                schema: "customer",
                table: "CustomerOrders",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "SubtotalAmountMinor",
                schema: "customer",
                table: "CustomerOrders",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "DiscountUnitAmountMinor",
                schema: "customer",
                table: "CustomerOrderItems",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "ListUnitPriceAmountMinor",
                schema: "customer",
                table: "CustomerOrderItems",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.Sql(
                """
                UPDATE customer.CustomerOrders
                SET SubtotalAmountMinor = TotalAmountMinor,
                    DiscountAmountMinor = 0
                WHERE SubtotalAmountMinor = 0
                """);

            migrationBuilder.Sql(
                """
                UPDATE customer.CustomerOrderItems
                SET ListUnitPriceAmountMinor = UnitPriceAmountMinor,
                    DiscountUnitAmountMinor = 0
                WHERE ListUnitPriceAmountMinor = 0
                """);

            migrationBuilder.CreateTable(
                name: "MenuPromotions",
                schema: "customer",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    DiscountKind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    DiscountValue = table.Column<int>(type: "int", nullable: false),
                    StartsAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndsAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DailyStartLocal = table.Column<TimeOnly>(type: "time", nullable: true),
                    DailyEndLocal = table.Column<TimeOnly>(type: "time", nullable: true),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MenuItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuPromotions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionOffers",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Audience = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    TargetPlanCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    DiscountPercent = table.Column<int>(type: "int", nullable: false),
                    DurationMonths = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    StartsAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndsAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionOffers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TenantNotifications",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Audience = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    StartsAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndsAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ActionUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LastDispatchedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantNotifications", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MenuPromotions_TenantId_BranchId_IsActive_StartsAtUtc",
                schema: "customer",
                table: "MenuPromotions",
                columns: new[] { "TenantId", "BranchId", "IsActive", "StartsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionOffers_Audience_IsActive_StartsAtUtc",
                schema: "billing",
                table: "SubscriptionOffers",
                columns: new[] { "Audience", "IsActive", "StartsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TenantNotifications_TenantId_Audience_IsActive_StartsAtUtc",
                schema: "billing",
                table: "TenantNotifications",
                columns: new[] { "TenantId", "Audience", "IsActive", "StartsAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MenuPromotions",
                schema: "customer");

            migrationBuilder.DropTable(
                name: "SubscriptionOffers",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "TenantNotifications",
                schema: "billing");

            migrationBuilder.DropColumn(
                name: "DiscountAmountMinor",
                schema: "customer",
                table: "CustomerOrders");

            migrationBuilder.DropColumn(
                name: "SubtotalAmountMinor",
                schema: "customer",
                table: "CustomerOrders");

            migrationBuilder.DropColumn(
                name: "DiscountUnitAmountMinor",
                schema: "customer",
                table: "CustomerOrderItems");

            migrationBuilder.DropColumn(
                name: "ListUnitPriceAmountMinor",
                schema: "customer",
                table: "CustomerOrderItems");
        }
    }
}
