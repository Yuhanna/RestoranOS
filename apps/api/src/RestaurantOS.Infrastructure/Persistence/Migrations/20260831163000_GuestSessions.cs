using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RestaurantOS.Infrastructure;

#nullable disable

namespace RestaurantOS.Infrastructure.Persistence.Migrations;

[DbContext(typeof(RestaurantOsDbContext))]
[Migration("20260831163000_GuestSessions")]
public partial class GuestSessions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "GuestSessions",
            schema: "customer",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TableSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DeviceIdentifier = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                IpHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                Locale = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                LastActivityAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                RiskScore = table.Column<int>(type: "int", nullable: false),
                Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_GuestSessions", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_GuestSessions_TableSessionId_Status",
            schema: "customer",
            table: "GuestSessions",
            columns: new[] { "TableSessionId", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_GuestSessions_TenantId_BranchId_LastActivityAtUtc",
            schema: "customer",
            table: "GuestSessions",
            columns: new[] { "TenantId", "BranchId", "LastActivityAtUtc" });

        migrationBuilder.AddColumn<Guid>(
            name: "GuestSessionId",
            schema: "customer",
            table: "CustomerOrders",
            type: "uniqueidentifier",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "GuestSessionId",
            schema: "customer",
            table: "CustomerOrders");

        migrationBuilder.DropTable(
            name: "GuestSessions",
            schema: "customer");
    }
}
