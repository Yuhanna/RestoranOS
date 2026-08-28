using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CustomerExperienceFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "customer");

            migrationBuilder.CreateTable(
                name: "CustomerOrders",
                schema: "customer",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TableId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RequestHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    DisplayNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    TotalAmountMinor = table.Column<long>(type: "bigint", nullable: false),
                    TotalCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EstimatedReadyAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerOrders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomerSessions",
                schema: "customer",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TableId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    Locale = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Menus",
                schema: "customer",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    PublishedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Menus", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tenants",
                schema: "customer",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomerOrderItems",
                schema: "customer",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MenuItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    UnitPriceAmountMinor = table.Column<long>(type: "bigint", nullable: false),
                    UnitPriceCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerOrderItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerOrderItems_CustomerOrders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "customer",
                        principalTable: "CustomerOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MenuCategories",
                schema: "customer",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MenuId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    PublishedMenuId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MenuCategories_Menus_PublishedMenuId",
                        column: x => x.PublishedMenuId,
                        principalSchema: "customer",
                        principalTable: "Menus",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MenuItems",
                schema: "customer",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MenuId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    PriceAmountMinor = table.Column<long>(type: "bigint", nullable: false),
                    PriceCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    IsAvailable = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    PublishedMenuId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MenuItems_Menus_PublishedMenuId",
                        column: x => x.PublishedMenuId,
                        principalSchema: "customer",
                        principalTable: "Menus",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Restaurants",
                schema: "customer",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Restaurants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Restaurants_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "customer",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Branches",
                schema: "customer",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Branches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Branches_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalSchema: "customer",
                        principalTable: "Restaurants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DiningTables",
                schema: "customer",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiningTables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiningTables_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "customer",
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TableQrCodes",
                schema: "customer",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TableId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TableQrCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TableQrCodes_DiningTables_TableId",
                        column: x => x.TableId,
                        principalSchema: "customer",
                        principalTable: "DiningTables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Branches_RestaurantId",
                schema: "customer",
                table: "Branches",
                column: "RestaurantId");

            migrationBuilder.CreateIndex(
                name: "IX_Branches_TenantId_Id",
                schema: "customer",
                table: "Branches",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerOrderItems_OrderId",
                schema: "customer",
                table: "CustomerOrderItems",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerOrders_CustomerSessionId_IdempotencyKey",
                schema: "customer",
                table: "CustomerOrders",
                columns: new[] { "CustomerSessionId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerOrders_TenantId_BranchId_CreatedAtUtc",
                schema: "customer",
                table: "CustomerOrders",
                columns: new[] { "TenantId", "BranchId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerSessions_TenantId_BranchId_ExpiresAtUtc",
                schema: "customer",
                table: "CustomerSessions",
                columns: new[] { "TenantId", "BranchId", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerSessions_TokenHash",
                schema: "customer",
                table: "CustomerSessions",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiningTables_BranchId",
                schema: "customer",
                table: "DiningTables",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_DiningTables_TenantId_BranchId_Label",
                schema: "customer",
                table: "DiningTables",
                columns: new[] { "TenantId", "BranchId", "Label" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MenuCategories_PublishedMenuId",
                schema: "customer",
                table: "MenuCategories",
                column: "PublishedMenuId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuCategories_TenantId_BranchId_MenuId_SortOrder",
                schema: "customer",
                table: "MenuCategories",
                columns: new[] { "TenantId", "BranchId", "MenuId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_PublishedMenuId",
                schema: "customer",
                table: "MenuItems",
                column: "PublishedMenuId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_TenantId_BranchId_MenuId_CategoryId",
                schema: "customer",
                table: "MenuItems",
                columns: new[] { "TenantId", "BranchId", "MenuId", "CategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_Menus_TenantId_BranchId_PublishedAtUtc",
                schema: "customer",
                table: "Menus",
                columns: new[] { "TenantId", "BranchId", "PublishedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Restaurants_TenantId_Id",
                schema: "customer",
                table: "Restaurants",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TableQrCodes_TableId",
                schema: "customer",
                table: "TableQrCodes",
                column: "TableId");

            migrationBuilder.CreateIndex(
                name: "IX_TableQrCodes_TenantId_BranchId_TableId",
                schema: "customer",
                table: "TableQrCodes",
                columns: new[] { "TenantId", "BranchId", "TableId" });

            migrationBuilder.CreateIndex(
                name: "IX_TableQrCodes_TokenHash",
                schema: "customer",
                table: "TableQrCodes",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerOrderItems",
                schema: "customer");

            migrationBuilder.DropTable(
                name: "CustomerSessions",
                schema: "customer");

            migrationBuilder.DropTable(
                name: "MenuCategories",
                schema: "customer");

            migrationBuilder.DropTable(
                name: "MenuItems",
                schema: "customer");

            migrationBuilder.DropTable(
                name: "TableQrCodes",
                schema: "customer");

            migrationBuilder.DropTable(
                name: "CustomerOrders",
                schema: "customer");

            migrationBuilder.DropTable(
                name: "Menus",
                schema: "customer");

            migrationBuilder.DropTable(
                name: "DiningTables",
                schema: "customer");

            migrationBuilder.DropTable(
                name: "Branches",
                schema: "customer");

            migrationBuilder.DropTable(
                name: "Restaurants",
                schema: "customer");

            migrationBuilder.DropTable(
                name: "Tenants",
                schema: "customer");
        }
    }
}
