using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantOS.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class ServiceRequests : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ServiceRequests",
            schema: "customer",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TableId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CustomerSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Type = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                Note = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ServiceRequests", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ServiceRequests_CustomerSessionId_Type_Status",
            schema: "customer",
            table: "ServiceRequests",
            columns: new[] { "CustomerSessionId", "Type", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_ServiceRequests_TenantId_BranchId_Status_CreatedAtUtc",
            schema: "customer",
            table: "ServiceRequests",
            columns: new[] { "TenantId", "BranchId", "Status", "CreatedAtUtc" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ServiceRequests",
            schema: "customer");
    }
}
